using System.Reflection;

namespace FastEndpoints;

internal record PropCacheEntry<TRequest>(
    string PropName,
    Type PropType,
    TypeCode PropTypeCode,
    Action<TRequest, object> PropSetter);

internal record FromClaimPropCacheEntry<TRequest>(
    string ClaimType,
    bool ForbidIfMissing,
    Action<TRequest, object> PropSetter);

internal static class ReqTypeCache<TRequest>
{
    //note: key is lowercased property name
    internal static Dictionary<string, PropCacheEntry<TRequest>> CachedProps { get; } = new();

    internal static List<FromClaimPropCacheEntry<TRequest>> CachedFromClaimProps { get; } = new();

    static ReqTypeCache()
    {
        foreach (var propInfo in typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            var propName = propInfo.Name;
            var compiledSetter = Tool.CompiledSetter<TRequest>(propName);

            CachedProps.Add(propName.ToLower(),
                new(
                    propName,
                    propInfo.PropertyType,
                    Type.GetTypeCode(propInfo.PropertyType),
                    compiledSetter));

            if (propInfo.IsDefined(typeof(FromClaimAttribute), false))
            {
                if (propInfo.PropertyType != typeof(string))
                    throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");
                //could add claim binding support for other types just like in route binding.

                var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
                var claimType = attrib?.ClaimType ?? "null";
                var forbidIfMissing = attrib?.IsRequired ?? false;

                CachedFromClaimProps.Add(new(claimType, forbidIfMissing, compiledSetter));
            }
        }
    }
}

