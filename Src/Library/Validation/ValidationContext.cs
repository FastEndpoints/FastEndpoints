using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// provides a way to manipulate the validation failures of the current endpoint context.
/// call <see cref="Instance"/> to obtain an instance of the current validation context.
/// </summary>
public class ValidationContext
{
    public static ValidationContext Instance => new();

    ///<inheritdoc/>
    public List<ValidationFailure> ValidationFailures { get; } =
        (List<ValidationFailure>?)
            Config.ServiceResolver?.TryResolve<IHttpContextAccessor>()?.HttpContext?.Items[CtxKey.ValidationFailures] ??
                new();

    ///<inheritdoc/>
    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    ///<inheritdoc/>
    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure);

    ///<inheritdoc/>
    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure)
        => ValidationFailures.ThrowError(failure);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(string message)
        => ValidationFailures.ThrowError(message);

    ///<inheritdoc/>
    public void ThrowIfAnyErrors() =>
        ValidationFailures.ThrowIfAnyErrors();
}

///<inheritdoc/>
public class ValidationContext<T> : ValidationContext, IValidationErrors<T>
{
    public static new ValidationContext<T> Instance => new();

    ///<inheritdoc/>
    public void AddError(Expression<Func<T, object>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(Expression<Func<T, object>> property, string errorMessage)
        => ValidationFailures.ThrowError(property, errorMessage);
}
