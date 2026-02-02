using Microsoft.Extensions.Logging;

namespace FastEndpoints.Swagger;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "No validators found in the system!")]
    public static partial void NoValidatorsFound(this ILogger l);

    [LoggerMessage(2, LogLevel.Error, "Exception while processing {validatorType}")]
    public static partial void ExceptionProcessingValidator(this ILogger l, Exception ex, string validatorType);

    [LoggerMessage(3, LogLevel.Warning, "Swagger with FluentValidation integration for polymorphic validators such as {polymorphicValidatorType} is not supported.")]
    public static partial void SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(this ILogger l, string polymorphicValidatorType);

    [LoggerMessage(4, LogLevel.Information, "Exporting swagger document: {documentName}")]
    public static partial void ExportingSwaggerDoc(this ILogger l, string documentName);

    [LoggerMessage(5, LogLevel.Information, "Swagger document '{documentName}' exported successfully to {filePath}")]
    public static partial void SwaggerDocExportSuccessful(this ILogger l, string documentName, string filePath);

    [LoggerMessage(6, LogLevel.Warning, "Failed to export swagger document '{documentName}': {errorMessage}")]
    public static partial void SwaggerDocExportFailed(this ILogger l, string documentName, string errorMessage);
}

/// <summary>
/// marker type for swagger export logging
/// </summary>
public class SwaggerExportRunner { }