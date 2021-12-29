using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

internal static class Types
{
    internal static readonly Type IFormFile = typeof(IFormFile);
    internal static readonly Type Guid = typeof(Guid);
    internal static readonly Type Enum = typeof(Enum);
    internal static readonly Type Uri = typeof(Uri);
    internal static readonly Type Version = typeof(Version);
    internal static readonly Type TimeSpan = typeof(TimeSpan);
}
