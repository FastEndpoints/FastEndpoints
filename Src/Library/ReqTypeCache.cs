using System.Reflection;

namespace FastEndpoints;

internal static class ReqTypeCache<TRequest>
{
    //key: property name
    internal static Dictionary<string, PropCacheEntry> CachedProps { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static List<FromClaimPropCacheEntry> CachedFromClaimProps { get; } = new();

    internal static List<FromHeaderPropCacheEntry> CachedFromHeaderProps { get; } = new();

    internal static bool IsPlainTextRequest;

    static ReqTypeCache()
    {
        var tRequest = typeof(TRequest);

        IsPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(tRequest);

        foreach (var propInfo in tRequest.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            if (!propInfo.CanRead || !propInfo.CanWrite)
                continue;

            if (IsPlainTextRequest && propInfo.Name == nameof(IPlainTextRequest.Content))
                continue;

            var compiledSetter = tRequest.SetterForProp(propInfo.Name);

            if (AddFromClaimPropCacheEntry(propInfo, compiledSetter))
                continue;

            if (AddFromHeaderPropCacheEntry(propInfo, compiledSetter))
                continue;

            AddPropCacheEntry(propInfo, compiledSetter);
        }
    }

    private static bool AddFromClaimPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
        if (attrib is not null)
        {
            var claimType = attrib?.ClaimType ?? propInfo.Name;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromClaimProps.Add(new(
                claimType,
                forbidIfMissing,
                propInfo.PropertyType,
                propInfo.PropertyType.ValueParser(),
                compiledSetter));

            return forbidIfMissing; //if claim is optional, return false so it will be added as a PropCacheEntry
        }
        return false;
    }

    private static bool AddFromHeaderPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromHeaderAttribute>(false);
        if (attrib is not null)
        {
            var headerName = attrib?.HeaderName ?? propInfo.Name;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromHeaderProps.Add(new(
                headerName,
                forbidIfMissing,
                propInfo.PropertyType,
                propInfo.PropertyType?.ValueParser(),
                compiledSetter));
            return forbidIfMissing; //if header is optional, return false so it will be added as a PropCacheEntry;
        }
        return false;
    }

    private static void AddPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        CachedProps.Add(propInfo.Name, new(
            propInfo.PropertyType,
            propInfo.PropertyType.ValueParser(),
            compiledSetter));
    }
}

internal record PropCacheEntry(
    Type PropType,
    Func<object?, (bool isSuccess, object value)>? ValueParser,
    Action<object, object> PropSetter);

internal record FromClaimPropCacheEntry(
    string ClaimType,
    bool ForbidIfMissing,
    Type PropType,
    Func<object?, (bool isSuccess, object value)>? ValueParser,
    Action<object, object> PropSetter);

internal record FromHeaderPropCacheEntry(
    string HeaderName,
    bool ForbidIfMissing,
    Type PropType,
    Func<object?, (bool isSuccess, object value)>? ValueParser,
    Action<object, object> PropSetter);