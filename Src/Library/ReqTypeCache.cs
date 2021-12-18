using System.Reflection;

namespace FastEndpoints;

internal record PropCacheEntry(
    string PropName,
    Type PropType,
    TypeCode PropTypeCode,
    Action<object, object> PropSetter);

internal record FromClaimPropCacheEntry(
    string ClaimType,
    bool ForbidIfMissing,
    Action<object, object> PropSetter);

internal static class ReqTypeCache<TRequest>
{
    //note: key is lowercased property name
    internal static Dictionary<string, PropCacheEntry> CachedProps { get; } = new();

    internal static List<FromClaimPropCacheEntry> CachedFromClaimProps { get; } = new();

    static ReqTypeCache()
    {
        var reqType = typeof(TRequest);

        foreach (var propInfo in reqType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            if (!propInfo.CanRead || !propInfo.CanWrite)
                return;

            var propName = propInfo.Name;
            var compiledSetter = reqType.SetterForProp(propName);

            CachedProps.Add(propName.ToLower(), new(
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
                var claimType = attrib?.ClaimType ?? propName;
                var forbidIfMissing = attrib?.IsRequired ?? false;

                CachedFromClaimProps.Add(new(claimType, forbidIfMissing, compiledSetter));
            }
        }
    }
}

