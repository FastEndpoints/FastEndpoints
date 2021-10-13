using System.Reflection;

namespace FastEndpoints;

internal record PropCacheEntry(
    PropertyInfo PropInfo,
    TypeCode TypeCode);

internal record FromClaimPropCacheEntry(string ClaimType, bool ForbidIfMissing, PropertyInfo PropInfo);

internal static class ReqTypeCache<TRequest>
{
    //note: key is lowercased property name
    internal static Dictionary<string, PropCacheEntry> CachedProps { get; } = new();

    internal static List<FromClaimPropCacheEntry> CachedFromClaimProps { get; } = new();

    static ReqTypeCache()
    {
        foreach (var propInfo in typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            var propName = propInfo.Name.ToLower();

            CachedProps.Add(propName, new(propInfo, Type.GetTypeCode(propInfo.PropertyType)));

            if (propInfo.IsDefined(typeof(FromClaimAttribute), false))
            {
                if (propInfo.PropertyType != typeof(string))
                    throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");
                //could add claim binding support for other types just like in route binding.

                var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
                var claimType = attrib?.ClaimType ?? "null";
                var forbidIfMissing = attrib?.IsRequired ?? false;

                CachedFromClaimProps.Add(new(claimType, forbidIfMissing, propInfo));
            }
        }
    }
}

