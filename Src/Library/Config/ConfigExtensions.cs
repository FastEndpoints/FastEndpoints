using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FastEndpoints;

static class ConfigExtensions
{
    internal static void ConfigureSerializer(this JsonSerializerOptions opts, Cfg cfg, Action<Cfg>? configAction)
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

                    //ignore dto props marks with [FromHeader] and the property type ends with 'HeaderValue'. see: AddTypedHeaderValueParsers
                    if (pi.AttributeProvider?.IsDefined(Types.FromHeaderAttribute, true) is true && pi.PropertyType.Name.EndsWith("HeaderValue"))
                        ti.Properties.RemoveAt(i);
                }
            });
        configAction?.Invoke(cfg);
        opts.TypeInfoResolverChain.Insert(0, new FastEndpointsSerializerContext(new(opts)));
    }
}