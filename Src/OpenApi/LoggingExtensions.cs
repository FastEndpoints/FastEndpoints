using Microsoft.Extensions.Logging;

namespace FastEndpoints.OpenApi;

static partial class LoggingExtensions
{
    [LoggerMessage(2, LogLevel.Error, "Exception while processing {validatorType}")]
    public static partial void ExceptionProcessingValidator(this ILogger l, Exception ex, string validatorType);

    [LoggerMessage(3, LogLevel.Warning, "Swagger with FluentValidation integration for polymorphic validators such as {polymorphicValidatorType} is not supported.")]
    public static partial void SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(this ILogger l, string polymorphicValidatorType);

    [LoggerMessage(4, LogLevel.Information, "Exporting OpenAPI document: {documentName}")]
    public static partial void ExportingOpenApiDoc(this ILogger l, string documentName);

    [LoggerMessage(5, LogLevel.Information, "OpenAPI document '{documentName}' exported successfully to {filePath}")]
    public static partial void OpenApiDocExportSuccessful(this ILogger l, string documentName, string filePath);

    [LoggerMessage(6, LogLevel.Error, "Failed to export OpenAPI document '{documentName}'")]
    public static partial void OpenApiDocExportFailed(this ILogger l, Exception ex, string documentName);

    [LoggerMessage(7, LogLevel.Warning, "Failed to apply FluentValidation rule for property '{propertyName}' using validator '{validatorName}'")]
    public static partial void FailedToApplyValidationRule(this ILogger l, Exception ex, string propertyName, string validatorName);
}

/// <summary>
/// marker type for openapi export logging
/// </summary>
public class OpenApiExportRunner { }