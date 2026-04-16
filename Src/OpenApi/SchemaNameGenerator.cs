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

    internal static Func<JsonTypeInfo, string?> Create(bool shortSchemaNames)
        => typeInfo => GetReferenceId(typeInfo.Type, shortSchemaNames);

    internal static string? GetReferenceId(Type type, bool shortSchemaNames)
    {
        if (ShouldInlineType(type))
            return null;

        return Generate(type, shortSchemaNames);
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

    static bool IsFormFileType(Type type)
    {
        // IFormFile and IFormFileCollection should be inlined, not referenced
        if (type.FullName is "Microsoft.AspNetCore.Http.IFormFile" or "Microsoft.AspNetCore.Http.IFormFileCollection")
            return true;

        // generic collections of IFormFile (List<IFormFile>, IEnumerable<IFormFile>, etc.) should also be inlined
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (arg.FullName == "Microsoft.AspNetCore.Http.IFormFile")
                    return true;
            }
        }

        return false;
    }

    static string Generate(Type type, bool shortNames)
    {
        var isGeneric = type.IsGenericType;
        var fullNameWithoutGenericArgs =
            isGeneric
                ? type.FullName![..type.FullName!.IndexOf('`')]
                : type.FullName ?? type.Name;

        if (shortNames)
        {
            var index = fullNameWithoutGenericArgs!.LastIndexOf('.');
            index = index == -1 ? 0 : index + 1;
            var shortName = fullNameWithoutGenericArgs[index..];

            return isGeneric
                       ? shortName + GenericArgString(type)
                       : shortName;
        }

        var sanitizedFullName = fullNameWithoutGenericArgs!.Replace(".", string.Empty).Replace("+", "_");

        return isGeneric
                   ? sanitizedFullName + GenericArgString(type)
                   : sanitizedFullName;
    }

    static string GenericArgString(Type type)
    {
        if (type.IsGenericType)
        {
            var sb = new StringBuilder();
            var args = type.GetGenericArguments();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (i == 0)
                    sb.Append("Of");
                sb.Append(TypeNameWithoutGenericArgs(arg));
                sb.Append(GenericArgString(arg));
                if (i < args.Length - 1)
                    sb.Append("And");
            }

            return sb.ToString();
        }

        return type.Name;

        static string TypeNameWithoutGenericArgs(Type type)
        {
            var index = type.Name.IndexOf('`');
            index = index == -1 ? 0 : index;

            return type.Name[..index];
        }
    }
}