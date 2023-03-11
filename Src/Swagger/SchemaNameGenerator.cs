using NJsonSchema.Generation;
using System.Text;

namespace FastEndpoints.Swagger;

internal sealed class SchemaNameGenerator : ISchemaNameGenerator
{
    private readonly bool shortSchemaNames;

    public SchemaNameGenerator(bool shortSchemaNames)
    {
        this.shortSchemaNames = shortSchemaNames;
    }

    public string? Generate(Type type)
    {
        var isGeneric = type.IsGenericType;
        var fullNameWithoutGenericArgs =
                isGeneric
                ? type.FullName![..type.FullName!.IndexOf('`')]
                : type.FullName;

        if (shortSchemaNames)
        {
            var index = fullNameWithoutGenericArgs!.LastIndexOf('.');
            index = index == -1 ? 0 : index + 1;
            var shortName = fullNameWithoutGenericArgs[index..];
            return isGeneric
                   ? shortName + GenericArgString(type)
                   : shortName;
        }
        else
        {
            var sanitizedFullName = fullNameWithoutGenericArgs!.Replace(".", string.Empty);
            return isGeneric
                   ? sanitizedFullName + GenericArgString(type)
                   : sanitizedFullName;
        }

        static string? GenericArgString(Type type)
        {
            if (type.IsGenericType)
            {
                var sb = new StringBuilder();
                var args = type.GetGenericArguments();
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (i == 0) sb.Append("Of");
                    sb.Append(TypeNameWithoutGenericArgs(arg));
                    sb.Append(GenericArgString(arg));
                    if (i < args.Length - 1) sb.Append("And");
                }
                return sb.ToString();
            }
            return type.Name;

            static string TypeNameWithoutGenericArgs(Type type)
            {
                var index = type.Name.IndexOf('`');
                index = index == -1 ? 0 : index;
                return type.Name![..index];
            }
        }
    }
}
