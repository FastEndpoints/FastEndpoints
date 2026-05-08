using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints.OpenApi;

sealed class HiddenPropertySchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : PropertyRemovalSchemaTransformer(docOpts, sharedCtx)
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _hiddenPropertiesCache = new();

    protected override IReadOnlyList<PropertyInfo> GetPropertiesToRemove(Type type)
        => _hiddenPropertiesCache.GetOrAdd(type, GetHiddenProperties);

    static PropertyInfo[] GetHiddenProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(static prop => prop.IsDefined(Types.HideFromDocsAttribute))
               .ToArray();
}
