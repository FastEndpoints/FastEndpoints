using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint, IValidationErrors<TRequest> where TRequest : notnull
{
    private static async Task ValidateRequest(TRequest req, EndpointDefinition def, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (def.ValidatorType is null)
            return;

        var valResult = await ((IValidator<TRequest>)def.GetValidator()!).ValidateAsync(req, cancellation);

        if (!valResult.IsValid)
            validationFailures.AddRange(valResult.Errors);

        if (validationFailures.Count > 0 && def.ThrowIfValidationFails)
            throw new ValidationFailureException(validationFailures, "Request validation failed");
    }

    ///<inheritdoc/>
    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    ///<inheritdoc/>
    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure);

    ///<inheritdoc/>
    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    ///<inheritdoc/>
    public void AddError(Expression<Func<TRequest, object>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure)
        => ValidationFailures.ThrowError(failure);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(string message)
        => ValidationFailures.ThrowError(message);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
        => ValidationFailures.ThrowError(property, errorMessage);

    ///<inheritdoc/>
    public void ThrowIfAnyErrors() =>
        ValidationFailures.ThrowIfAnyErrors();
}