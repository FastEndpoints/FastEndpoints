using System.Reflection;

namespace FastEndpoints.OpenApi;

static class ConstructorDefaultExtensions
{
    internal static object? GetParentCtorDefaultValue(this PropertyInfo property)
    {
        var parentType = property.DeclaringType;

        if (parentType?.IsClass is not true)
            return null;

        return parentType.GetConstructors()
                         .Select(c => c.GetParameters())
                         .MaxBy(p => p.Length)?
                         .SingleOrDefault(
                             p => p.HasDefaultValue &&
                                  p.Name?.Equals(property.Name, StringComparison.OrdinalIgnoreCase) is true)?.DefaultValue;
    }
}
