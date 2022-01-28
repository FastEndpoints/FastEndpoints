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
            if (propInfo.PropertyType != Types.String)
                throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");
            //could add claim binding support for other types just like in route binding.

            var claimType = attrib?.ClaimType ?? propInfo.Name;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromClaimProps.Add(new(claimType, forbidIfMissing, compiledSetter));
            return true;
        }
        return false;
    }

    private static bool AddFromHeaderPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromHeaderAttribute>(false);
        if (attrib is not null)
        {
            if (propInfo.PropertyType != Types.String)
                throw new InvalidOperationException("[FromHeader] attributes are only supported on string properties!");
            //could add header binding support for other types just like in route binding.

            var headerName = attrib?.HeaderName ?? propInfo.Name;
            var forbidIfMissing = attrib?.IsRequired ?? false;

            CachedFromHeaderProps.Add(new(headerName, forbidIfMissing, compiledSetter));
            return true;
        }
        return false;
    }

    private static void AddPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        Func<object?, (bool isSuccess, object value)>? valParser = null;

        var tProp = propInfo.PropertyType;
        
        if (propInfo.PropertyType.IsEnum)
        {
            valParser = input => (Enum.TryParse(tProp, (string?)input, out var res), res!);
        }
        else
        {
            switch (Type.GetTypeCode(propInfo.PropertyType))
            {
                case TypeCode.String:
                    valParser = input => (true, input!);
                    break;

                case TypeCode.Boolean:
                    valParser = input => (bool.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.Int32:
                    valParser = input => (int.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.Int64:
                    valParser = input => (long.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.Double:
                    valParser = input => (double.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.Decimal:
                    valParser = input => (decimal.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.DateTime:
                    valParser = input => (DateTime.TryParse((string?)input, out var res), res);
                    break;

                case TypeCode.Object:
                    if (tProp == Types.Guid)
                    {
                        valParser = input => (Guid.TryParse((string?)input, out var res), res);
                    }
                    else if (tProp == Types.Uri)
                    {
                        valParser = input => (true, new Uri((string)input!));
                    }
                    else if (tProp == Types.Version)
                    {
                        valParser = input => (Version.TryParse((string?)input, out var res), res!);
                    }
                    else if (tProp == Types.TimeSpan)
                    {
                        valParser = input => (TimeSpan.TryParse((string?)input, out var res), res!);
                    }
                    break;
            }
        }

        CachedProps.Add(propInfo.Name, new(
            propInfo.PropertyType,
            valParser,
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
    Action<object, object> PropSetter);

internal record FromHeaderPropCacheEntry(
    string HeaderName,
    bool ForbidIfMissing,
    Action<object, object> PropSetter);