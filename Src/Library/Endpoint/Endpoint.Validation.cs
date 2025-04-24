using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : IValidationErrors<TRequest> where TRequest : notnull
{
    static async Task ValidateRequest(TRequest req, EndpointDefinition def, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (def.ValidatorType is null)
            return;

        var valResult = await ((IValidator<TRequest>)def.GetValidator()!).ValidateAsync(req, cancellation);

        if (!valResult.IsValid)
        {
            for (var i = 0; i < valResult.Errors.Count; i++)
                validationFailures.AddError(valResult.Errors[i], def.ReqDtoFromBodyPropName);
        }

        if (validationFailures.Count > 0 && def.ThrowIfValidationFails)
            throw new ValidationFailureException(validationFailures, "Request validation failed");
    }

    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure, Definition.ReqDtoFromBodyPropName);

    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    public void AddError(Expression<Func<TRequest, object?>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure, int? statusCode = null)
        => ValidationFailures.ThrowError(failure, statusCode, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(string message, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message);

    [DoesNotReturn]
    public void ThrowError(string message, string errorCode, Severity severity = Severity.Error, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message, errorCode, severity);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object?>> property, string errorMessage, int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage, null, default, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object?>> property,
                           string errorMessage,
                           string errorCode,
                           Severity severity = Severity.Error,
                           int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage, errorCode, severity, Definition.ReqDtoFromBodyPropName);

    public void ThrowIfAnyErrors(int? statusCode = null)
        => ValidationFailures.ThrowIfAnyErrors(statusCode);
}