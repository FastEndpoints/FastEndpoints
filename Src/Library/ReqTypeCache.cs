using System.Reflection;

namespace FastEndpoints;

internal static class ReqTypeCache<TRequest>
{
    //key: property name
    internal static Dictionary<string, PropCacheEntry> CachedProps { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static List<FromClaimPropCacheEntry> CachedFromClaimProps { get; } = new();

    internal static List<FromHeaderPropCacheEntry> CachedFromHeaderProps { get; } = new();

    static ReqTypeCache()
    {
        var tRequest = typeof(TRequest);

        foreach (var propInfo in tRequest.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            if (!propInfo.CanRead || !propInfo.CanWrite)
                continue;

            var propName = propInfo.Name;
            var compiledSetter = tRequest.SetterForProp(propName);

            if (AddFromClaimPropCacheEntry(propInfo, propName, compiledSetter))
                continue;

            if (AddFromHeaderPropCacheEntry(propInfo, propName, compiledSetter))
                continue;

            AddPropCacheEntry(propInfo, propName, compiledSetter);
        }
    }

    private static bool AddFromClaimPropCacheEntry(PropertyInfo propInfo, string propName, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
        if (attrib is not null)
        {
            if (propInfo.PropertyType != typeof(string))
                throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");
            //could add claim binding support for other types just like in route binding.

            var claimType = attrib?.ClaimType ?? propName;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromClaimProps.Add(new(claimType, forbidIfMissing, compiledSetter));
            return true;
        }
        return false;
    }

    private static bool AddFromHeaderPropCacheEntry(PropertyInfo propInfo, string propName, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromHeaderAttribute>(false);
        if (attrib is not null)
        {
            if (propInfo.PropertyType != typeof(string))
                throw new InvalidOperationException("[FromHeader] attributes are only supported on string properties!");
            //could add header binding support for other types just like in route binding.

            var headerName = attrib?.HeaderName ?? propName;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromHeaderProps.Add(new(headerName, forbidIfMissing, compiledSetter));
            return true;
        }
        return false;
    }

    private static void AddPropCacheEntry(PropertyInfo propInfo, string propName, Action<object, object> compiledSetter)
    {
        CachedProps.Add(propName, new(
            propName,
            propInfo.PropertyType,
            Type.GetTypeCode(propInfo.PropertyType),
            compiledSetter));
    }
}

internal record PropCacheEntry(
    string PropName,
    Type PropType,
    TypeCode PropTypeCode,
    Action<object, object> PropSetter);

internal record FromClaimPropCacheEntry(
    string ClaimType,
    bool ForbidIfMissing,
    Action<object, object> PropSetter);

internal record FromHeaderPropCacheEntry(
    string HeaderName,
    bool ForbidIfMissing,
    Action<object, object> PropSetter);