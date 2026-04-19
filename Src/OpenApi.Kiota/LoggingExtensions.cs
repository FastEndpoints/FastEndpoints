using Microsoft.Extensions.Logging;

namespace FastEndpoints.OpenApi.Kiota;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Exporting OpenAPI Spec for doc: [{documentName}]")]
    public static partial void ExportingOpenApiSpec(this ILogger l, string documentName);

    [LoggerMessage(2, LogLevel.Information, "OpenAPI Spec export successful!")]
    public static partial void OpenApiSpecExportSuccessful(this ILogger l);

    [LoggerMessage(3, LogLevel.Information, "Starting [{language}] Api Client generation for [{openApiDocumentName}]")]
    public static partial void StartingApiClientGeneration(this ILogger l, string language, string openApiDocumentName);

    [LoggerMessage(4, LogLevel.Information, "Api Client generation successful!")]
    public static partial void ApiClientGenerationSuccessful(this ILogger l);

    [LoggerMessage(5, LogLevel.Information, "Zipping up the generated client file...")]
    public static partial void ZippingGeneratedClientFile(this ILogger l);

    [LoggerMessage(6, LogLevel.Information, "Client archive creation successful!")]
    public static partial void ClientArchiveCreationSuccessful(this ILogger l);
}