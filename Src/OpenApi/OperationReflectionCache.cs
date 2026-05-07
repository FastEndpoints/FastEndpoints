using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace FastEndpoints.OpenApi;

static class OperationReflectionCache
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();
    static readonly ConcurrentDictionary<PropertyInfo, PropertyMetadata> _propertyMetadataCache = new();
    static readonly ConcurrentDictionary<PropertyInfo, bool> _nullablePropertyCache = new();

    internal sealed class TypeMetadata
    {
        public required PropertyInfo[] PublicInstanceProperties { get; init; }
        public required PropertyInfo[] BindableRequestProperties { get; init; }
    }

    internal sealed class PropertyMetadata
    {
        public required bool HasPublicGetter { get; init; }
        public required bool HasPublicSetter { get; init; }
        public required bool IsJsonIgnoredAlways { get; init; }
        public required bool IsHiddenFromDocs { get; init; }
        public required bool IsDontInject { get; init; }
        public required bool IsFromQuery { get; init; }
        public required bool IsFromBody { get; init; }
        public required bool IsFromForm { get; init; }
        public required FromClaimAttribute? FromClaim { get; init; }
        public required HasPermissionAttribute? HasPermission { get; init; }
        public required DontBindAttribute? DontBind { get; init; }
        public required BindFromAttribute? BindFrom { get; init; }
        public required FromHeaderAttribute? FromHeader { get; init; }
        public required FromCookieAttribute? FromCookie { get; init; }
        public required ToHeaderAttribute? ToHeader { get; init; }
        public required System.ComponentModel.DefaultValueAttribute? DefaultValue { get; init; }
    }

    internal static TypeMetadata GetTypeMetadata(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return _typeMetadataCache.GetOrAdd(type, CreateTypeMetadata);
    }

    internal static PropertyMetadata GetPropertyMetadata(PropertyInfo property)
        => _propertyMetadataCache.GetOrAdd(property, CreatePropertyMetadata);

    internal static bool IsNullable(PropertyInfo prop)
        => _nullablePropertyCache.GetOrAdd(prop, static property => new NullabilityInfoContext().Create(property).WriteState is NullabilityState.Nullable);

    static TypeMetadata CreateTypeMetadata(Type type)
    {
        var properties = type.GetProperties(PublicInstanceHierarchy);

        return new()
        {
            PublicInstanceProperties = properties,
            BindableRequestProperties = properties.Where(IsBindableRequestProperty).ToArray()
        };
    }

    static PropertyMetadata CreatePropertyMetadata(PropertyInfo property)
        => new()
        {
            HasPublicGetter = property.GetGetMethod()?.IsPublic is true,
            HasPublicSetter = property.GetSetMethod()?.IsPublic is true,
            IsJsonIgnoredAlways = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always,
            IsHiddenFromDocs = property.IsDefined(Types.HideFromDocsAttribute),
            IsDontInject = property.IsDefined(Types.DontInjectAttribute),
            IsFromQuery = property.IsDefined(typeof(FromQueryAttribute), false),
            IsFromBody = property.IsDefined(Types.FromBodyAttribute, false),
            IsFromForm = property.IsDefined(Types.FromFormAttribute, false),
            FromClaim = property.GetCustomAttribute<FromClaimAttribute>(),
            HasPermission = property.GetCustomAttribute<HasPermissionAttribute>(),
            DontBind = property.GetCustomAttribute<DontBindAttribute>(),
            BindFrom = property.GetCustomAttribute<BindFromAttribute>(),
            FromHeader = property.GetCustomAttribute<FromHeaderAttribute>(),
            FromCookie = property.GetCustomAttribute<FromCookieAttribute>(),
            ToHeader = property.GetCustomAttribute<ToHeaderAttribute>(),
            DefaultValue = property.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()
        };

    static bool IsBindableRequestProperty(PropertyInfo property)
    {
        var metadata = GetPropertyMetadata(property);

        return metadata is { HasPublicSetter: true, HasPublicGetter: true, IsJsonIgnoredAlways: false, IsDontInject: false };
    }
}
