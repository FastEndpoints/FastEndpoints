using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints.OpenApi;

/// <summary>
/// removes properties decorated with [ToHeader] from object schemas.
/// these properties are sent as response headers, not in the JSON body,
/// so they should not appear in the schema.
/// </summary>
sealed class ToHeaderPropertySchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : PropertyRemovalSchemaTransformer(docOpts, sharedCtx)
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _toHeaderPropertiesCache = new();

    protected override IReadOnlyList<PropertyInfo> GetPropertiesToRemove(Type type)
        => _toHeaderPropertiesCache.GetOrAdd(type, GetToHeaderProperties);

    static PropertyInfo[] GetToHeaderProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(static prop => prop.IsDefined(typeof(ToHeaderAttribute), true))
               .ToArray();
}
