using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FastEndpoints;

static class ConfigExtensions
{
    internal static void ConfigureSerializer(this JsonSerializerOptions opts)
    {
        opts.TypeInfoResolver = opts.TypeInfoResolver?.WithAddedModifier(
            ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                    return;

                for (var i = 0; i < ti.Properties.Count; i++)
                {
                    var pi = ti.Properties[i];

                    //make non-json binding source props not required
                    if (pi.AttributeProvider?.IsDefined(Types.NonJsonBindingAttribute, true) is true)
                        pi.IsRequired = false;

                    //ignore dto props marked with [ToHeader]
                    if (pi.AttributeProvider?.IsDefined(Types.ToHeaderAttribute, true) is true)
                        pi.ShouldSerialize = (_, _) => false;
                }
            });

        var ctx = new FastEndpointsSerializerContext(new(opts));
        Cfg.SerOpts.AspNetCoreOptions?.TypeInfoResolverChain.Insert(0, ctx); // to make IResults serialization use the ctx
        opts.TypeInfoResolverChain.Insert(0, ctx);
    }
}