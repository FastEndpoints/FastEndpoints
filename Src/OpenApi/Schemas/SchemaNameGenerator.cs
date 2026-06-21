using System.Text;
using System.Text.Json.Serialization.Metadata;

namespace FastEndpoints.OpenApi;

static class SchemaNameGenerator
{
    static readonly HashSet<Type> _inlinedSimpleTypes =
    [
        typeof(string),
        typeof(decimal),
        typeof(object),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(byte[])
    ];

    static readonly HashSet<Type> _inlinedNullableValueTypes =
    [
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid)
    ];

    static readonly SchemaNameRegistry _defaultRegistry = new();

    internal static Func<JsonTypeInfo, string?> Create(bool shortSchemaNames, SchemaNameRegistry registry)
        => typeInfo => GetReferenceId(typeInfo.Type, shortSchemaNames, registry);

    internal static string? GetReferenceId(Type type, bool shortSchemaNames)
        => GetReferenceId(type, shortSchemaNames, _defaultRegistry);

    internal static string? GetReferenceId(Type type, bool shortSchemaNames, SchemaNameRegistry registry)
    {
        if (type.GetUnderlyingType() is { IsEnum: true } enumType)
            type = enumType;

        return ShouldInlineType(type)
                   ? null
                   : registry.GetOrAdd(type, shortSchemaNames);
    }

    static bool ShouldInlineType(Type type)
    {
        // primitive/simple types don't get schema references
        if (type.IsPrimitive || _inlinedSimpleTypes.Contains(type))
            return true;

        // nullable value types of primitives
        var underlying = Nullable.GetUnderlyingType(type);

        if (underlying is not null && (underlying.IsPrimitive || _inlinedNullableValueTypes.Contains(underlying)))
            return true;

        // IFormFile types should be inlined as type:string/format:binary
        return IsFormFileType(type);
    }

    internal static bool IsFormFileType(Type type)

        // Generic collections of IFormFile should also be inlined.
        => type.FullName is "Microsoft.AspNetCore.Http.IFormFile" or "Microsoft.AspNetCore.Http.IFormFileCollection" ||
           (type.IsGenericType && type.GetGenericArguments().Any(static arg => arg.FullName == "Microsoft.AspNetCore.Http.IFormFile"));

    internal static string Generate(Type type, bool shortNames)
    {
        if (type.IsArray)
            return TypeNameWithoutGenericArgs(type.GetElementType()!, shortNames) + "Array";

        var isGeneric = type.IsGenericType;
        var fullNameWithoutGenericArgs =
            isGeneric
                ? type.FullName![..type.FullName!.IndexOf('`')]
                : type.FullName ?? type.Name;

        if (shortNames)
        {
            var index = fullNameWithoutGenericArgs.LastIndexOf('.');
            index = index == -1 ? 0 : index + 1;
            var shortName = fullNameWithoutGenericArgs[index..].Replace("+", "_");

            return isGeneric
                       ? shortName + GenericArgString(type, shortNames)
                       : shortName;
        }

        var sanitizedFullName = SanitizeFullName(fullNameWithoutGenericArgs);

        return isGeneric
                   ? sanitizedFullName + GenericArgString(type, shortNames)
                   : sanitizedFullName;
    }

    static string GenericArgString(Type type, bool shortNames)
    {
        if (!type.IsGenericType)
            return string.Empty;

        var sb = new StringBuilder();
        var args = type.GetGenericArguments();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (i == 0)
                sb.Append("Of");
            sb.Append(TypeNameWithoutGenericArgs(arg, shortNames));
            sb.Append(GenericArgString(arg, shortNames));
            if (i < args.Length - 1)
                sb.Append("And");
        }

        return sb.ToString();
    }

    static string TypeNameWithoutGenericArgs(Type type, bool shortNames)
    {
        if (type.IsArray)
            return TypeNameWithoutGenericArgs(type.GetElementType()!, shortNames) + "Array";

        if (shortNames)
        {
            var index = type.Name.IndexOf('`');

            return index == -1 ? SanitizeFullName(type.Name) : SanitizeFullName(type.Name[..index]);
        }

        var fullName = type.FullName ?? type.Name;
        var genericArgIndex = fullName.IndexOf('`');
        var fullNameWithoutGenericArgs = genericArgIndex == -1 ? fullName : fullName[..genericArgIndex];

        return SanitizeFullName(fullNameWithoutGenericArgs);
    }

    static string SanitizeFullName(string fullName)
    {
        var sb = new StringBuilder(fullName.Length);

        foreach (var ch in fullName)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')
                sb.Append(ch);
            else if (ch == '+')
                sb.Append('_');
        }

        return sb.ToString();
    }
}

sealed class SchemaNameRegistry
{
    readonly Lock _lock = new();
    readonly Dictionary<SchemaReferenceIdKey, string> _referenceIds = [];
    readonly Dictionary<SchemaReferenceNameKey, Type> _schemaNameOwners = [];

    internal string GetOrAdd(Type type, bool shortSchemaNames)
    {
        var cacheKey = new SchemaReferenceIdKey(type, shortSchemaNames);

        lock (_lock)
        {
            if (_referenceIds.TryGetValue(cacheKey, out var existingRefId))
                return existingRefId;

            var refId = CreateAvailableReferenceId(type, shortSchemaNames);
            _referenceIds[cacheKey] = refId;

            return refId;
        }
    }

    string CreateAvailableReferenceId(Type type, bool shortSchemaNames)
    {
        var baseRefId = SchemaNameGenerator.Generate(type, shortSchemaNames);

        if (TryClaim(baseRefId, type, shortSchemaNames))
            return baseRefId;

        for (var suffix = 2;; suffix++)
        {
            var candidate = baseRefId + suffix;

            if (TryClaim(candidate, type, shortSchemaNames))
                return candidate;
        }
    }

    bool TryClaim(string refId, Type type, bool shortSchemaNames)
    {
        var key = new SchemaReferenceNameKey(refId, shortSchemaNames);

        if (_schemaNameOwners.TryGetValue(key, out var owner))
            return owner == type;

        _schemaNameOwners[key] = type;

        return true;
    }

    // ReSharper disable  NotAccessedPositionalProperty.Local

    readonly record struct SchemaReferenceIdKey(Type Type, bool ShortSchemaNames);

    readonly record struct SchemaReferenceNameKey(string ReferenceId, bool ShortSchemaNames);
}