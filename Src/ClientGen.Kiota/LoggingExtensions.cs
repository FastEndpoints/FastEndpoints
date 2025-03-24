using Microsoft.Extensions.Logging;

namespace FastEndpoints.ClientGen.Kiota;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Exporting Swagger Spec for doc: [{documentName}]")]
    public static partial void ExportingSwaggerSpec(this ILogger l, string documentName);

    [LoggerMessage(2, LogLevel.Information, "Swagger Spec export successful!")]
    public static partial void SwaggerSpecExportSuccessful(this ILogger l);

    [LoggerMessage(3, LogLevel.Information, "Starting [{language}] Api Client generation for [{swaggerDocumentName}]")]
    public static partial void StartingApiClientGeneration(this ILogger l, string language, string swaggerDocumentName);

    [LoggerMessage(4, LogLevel.Information, "Api Client generation successful!")]
    public static partial void ApiClientGenerationSuccessful(this ILogger l);

    [LoggerMessage(5, LogLevel.Information, "Zipping up the generated client file...")]
    public static partial void ZippingGeneratedClientFile(this ILogger l);

    [LoggerMessage(6, LogLevel.Information, "Client archive creation successful!")]
    public static partial void ClientArchiveCreationSuccessful(this ILogger l);
}
