using Microsoft.Extensions.Logging;

namespace FastEndpoints.ClientGen;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Api client generation starting...")]
    public static partial void ApiClientGenerationStarting(this ILogger l);

    [LoggerMessage(2, LogLevel.Information, "{clientType} api client generation successful!")]
    public static partial void ApiClientGenerationSuccessful(this ILogger l, string clientType);

    [LoggerMessage(3, LogLevel.Information, "Exporting swagger json for doc: [{documentName}]")]
    public static partial void ExportingSwaggerJson(this ILogger l, string documentName);

    [LoggerMessage(4, LogLevel.Information, "Swagger json export successful!")]
    public static partial void SwaggerJsonExportSuccessful(this ILogger l);
}