namespace FastEndpoints;

internal static class EndpointExtensions
{
    internal static string ActualName(this Type type)
        => (Nullable.GetUnderlyingType(type) ?? type).Name;
}
