using System.Reflection;

namespace FastEndpoints.Swagger.ValidationProcessor.Extensions;

internal static class ReflectionExtension
{
    internal static bool IsSubClassOfGeneric(this Type? child, Type parent)
    {
        if (child == parent)
            return false;

        if (child?.IsSubclassOf(parent) is true)
            return true;

        var parameters = parent.GetGenericArguments();

        var isParameterLessGeneric = !(parameters is { Length: > 0 } &&
                                       (parameters[0].Attributes & TypeAttributes.BeforeFieldInit) == TypeAttributes.BeforeFieldInit);

        while (child != null && child != typeof(object))
        {
            var cur = GetFullTypeDefinition(child);

            if (parent == cur || (isParameterLessGeneric && cur.GetInterfaces()
                                                               .Select(GetFullTypeDefinition)
                                                               .Contains(GetFullTypeDefinition(parent))))
            {
                return true;
            }

            if (!isParameterLessGeneric)
            {
                if (GetFullTypeDefinition(parent) == cur && !cur.IsInterface)
                {
                    if (VerifyGenericArguments(GetFullTypeDefinition(parent), cur) && VerifyGenericArguments(parent, child))
                        return true;
                }
                else
                {
                    if (child.GetInterfaces()
                             .Where(i => GetFullTypeDefinition(parent) == GetFullTypeDefinition(i))
                             .Any(item => VerifyGenericArguments(parent, item)))
                    {
                        return true;
                    }
                }
            }

            child = child.BaseType;
        }

        return false;
    }

    private static Type GetFullTypeDefinition(Type type)
        => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static bool VerifyGenericArguments(Type parent, Type child)
    {
        var childArguments = child.GetGenericArguments();
        var parentArguments = parent.GetGenericArguments();

        if (childArguments.Length != parentArguments.Length)
            return true;

        for (var i = 0; i < childArguments.Length; i++)
        {
            if (childArguments[i].Assembly == parentArguments[i].Assembly &&
                childArguments[i].Name == parentArguments[i].Name &&
                childArguments[i].Namespace == parentArguments[i].Namespace)
            {
                continue;
            }

            if (!childArguments[i].IsSubclassOf(parentArguments[i]))
                return false;
        }

        return true;
    }
}