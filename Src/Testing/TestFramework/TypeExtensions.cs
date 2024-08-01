using System.Reflection;

namespace System;

public static class TypeExtensions
{
    public static bool IsAssignableToGenericType(this Type? givenType, Type? genericType)
        => givenType is not null &&
           genericType is not null &&
           (givenType == genericType ||
            givenType.MapsToGenericTypeDefinition(genericType) ||
            givenType.HasInterfaceThatMapsToGenericTypeDefinition(genericType) ||
            givenType.GetTypeInfo().BaseType?.IsAssignableToGenericType(genericType) is true);

    static bool HasInterfaceThatMapsToGenericTypeDefinition(this Type? givenType, Type? genericType)
        => givenType is not null &&
           genericType is not null &&
           givenType.GetTypeInfo()
                    .GetInterfaces()
                    .Where(it => it.GetTypeInfo().IsGenericType)
                    .Any(it => it.GetGenericTypeDefinition() == genericType);

    static bool MapsToGenericTypeDefinition(this Type? givenType, Type? genericType)
        => givenType is not null &&
           genericType?.GetTypeInfo().IsGenericTypeDefinition == true &&
           givenType.GetTypeInfo().IsGenericType &&
           givenType.GetGenericTypeDefinition() == genericType;
}