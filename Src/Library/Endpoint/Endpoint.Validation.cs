using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : IValidationErrors<TRequest> where TRequest : notnull
{
    static async Task ValidateRequest(TRequest req,
                                      EndpointDefinition def,
                                      List<ValidationFailure> validationFailures,
                                      CancellationToken cancellation)
    {
        if (def.ValidatorType is null)
            return;

        var valResult = await ((IValidator<TRequest>)def.GetValidator()!).ValidateAsync(req, cancellation);

        if (!valResult.IsValid)
        {
            validationFailures.AddRange(valResult.Errors);

            if (def.ReqDtoFromBodyPropName().Length != 0)
            {
                foreach (var f in validationFailures.Where(f => f.PropertyName.StartsWith(def.ReqDtoFromBodyPropName())))
                {
                    f.PropertyName = f.PropertyName.Substring(
                        def.ReqDtoFromBodyPropName().Length + 1,
                        f.PropertyName.Length - def.ReqDtoFromBodyPropName().Length - 1);
                }
            }
        }

        if (validationFailures.Count > 0 && def.ThrowIfValidationFails)
            throw new ValidationFailureException(validationFailures, "Request validation failed");
    }

    /// <inheritdoc />
    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    /// <inheritdoc />
    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure);

    /// <inheritdoc />
    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    /// <inheritdoc />
    public void AddError(Expression<Func<TRequest, object?>> property,
                         string errorMessage,
                         string? errorCode = null,
                         Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    /// <inheritdoc />
    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure, int? statusCode = null)
        => ValidationFailures.ThrowError(failure, statusCode);

    /// <inheritdoc />
    [DoesNotReturn]
    public void ThrowError(string message, int? statusCode = null)
        => ValidationFailures.ThrowError(message, statusCode);

    /// <inheritdoc />
    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object?>> property, string errorMessage, int? statusCode = null)
        => ValidationFailures.ThrowError(property, errorMessage, statusCode);

    /// <inheritdoc />
    public void ThrowIfAnyErrors(int? statusCode = null)
        => ValidationFailures.ThrowIfAnyErrors(statusCode);
}