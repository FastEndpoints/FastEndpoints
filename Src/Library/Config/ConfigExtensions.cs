using System.Text.Json;
#if NET8_0_OR_GREATER
using System.Text.Json.Serialization.Metadata;
#endif

namespace FastEndpoints;

static class ConfigExtensions
{
#if NET8_0_OR_GREATER
    internal static void IgnoreToHeaderAttributes(this JsonSerializerOptions opts)
    {
        opts.TypeInfoResolver = opts.TypeInfoResolver?.WithAddedModifier(
            ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                    return;

                for (var i = 0; i < ti.Properties.Count; i++)
                {
                    var pi = ti.Properties[i];
                    if (pi.AttributeProvider?.GetCustomAttributes(Types.ToHeaderAttribute, true).Length != 0)
                        pi.ShouldSerialize = (_, _) => false;
                }
            });
    }
#endif
}