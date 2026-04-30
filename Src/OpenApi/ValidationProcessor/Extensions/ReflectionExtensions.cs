using System.Reflection;

namespace FastEndpoints.OpenApi.ValidationProcessor.Extensions;

static class ReflectionExtension
{
    internal static bool IsSubClassOfGeneric(this Type? child, Type parent)
    {
        if (child == parent)
            return false;

        if (child?.IsSubclassOf(parent) is true)
            return true;

        var parameters = parent.GetGenericArguments();
        var parentDefinition = GetFullTypeDefinition(parent);

        var isParameterLessGeneric = !(parameters is { Length: > 0 } &&
                                       (parameters[0].Attributes & TypeAttributes.BeforeFieldInit) == TypeAttributes.BeforeFieldInit);

        while (child != null && child != typeof(object))
        {
            var cur = GetFullTypeDefinition(child);

            if (parent == cur ||
                (isParameterLessGeneric &&
                 cur.GetInterfaces()
                    .Any(i => GetFullTypeDefinition(i) == parentDefinition)))
                return true;

            if (!isParameterLessGeneric)
            {
                if (parentDefinition == cur && !cur.IsInterface)
                {
                    if (VerifyGenericArguments(parentDefinition, cur) && VerifyGenericArguments(parent, child))
                        return true;
                }
                else
                {
                    if (child.GetInterfaces()
                             .Any(i => parentDefinition == GetFullTypeDefinition(i) && VerifyGenericArguments(parent, i)))
                        return true;
                }
            }

            child = child.BaseType;
        }

        return false;
    }

    static Type GetFullTypeDefinition(Type type)
        => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    static bool VerifyGenericArguments(Type parent, Type child)
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
                continue;

            if (!childArguments[i].IsSubclassOf(parentArguments[i]))
                return false;
        }

        return true;
    }
}