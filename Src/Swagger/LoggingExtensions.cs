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
}