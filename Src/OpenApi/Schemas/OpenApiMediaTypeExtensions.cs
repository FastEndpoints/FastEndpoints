#if NET11_0_OR_GREATER
global using OpenApiMediaTypeValue = Microsoft.OpenApi.IOpenApiMediaType;
#else
global using OpenApiMediaTypeValue = Microsoft.OpenApi.OpenApiMediaType;
#endif
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiMediaTypeExtensions
{
    extension(IDictionary<string, OpenApiMediaTypeValue> content)
    {
        internal OpenApiMediaType GetMutableMediaType(string contentType)
        {
#if NET11_0_OR_GREATER
            var mediaType = content[contentType];

            if (mediaType is OpenApiMediaType concrete)
                return concrete;

            if (mediaType is not OpenApiMediaTypeReference { Target: OpenApiMediaType target })
                throw new InvalidOperationException("Cannot modify an unresolved OpenAPI media type reference.");

            // Localize referenced media types before changing an operation's schema or examples.
            var localized = new OpenApiMediaType(target);
            content[contentType] = localized;

            return localized;
#else
            return content[contentType];
#endif
        }

        internal IEnumerable<OpenApiMediaType> GetMutableMediaTypes()
        {
#if NET11_0_OR_GREATER
            foreach (var contentType in content.Keys.ToArray())
                yield return content.GetMutableMediaType(contentType);
#else
            return content.Values;
#endif
        }
    }
}
