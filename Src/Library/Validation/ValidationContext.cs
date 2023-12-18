using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// provides a way to manipulate the validation failures of the current endpoint context.
/// call <see cref="Instance" /> to obtain an instance of the current validation context.
/// </summary>
public class ValidationContext
{
    public static ValidationContext Instance => new();

    public List<ValidationFailure> ValidationFailures { get; } =
        (List<ValidationFailure>?)Cfg.ServiceResolver?.TryResolve<IHttpContextAccessor>()?.HttpContext?.Items[CtxKey.ValidationFailures] ?? new();

    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure);

    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure, int? statusCode = null)
        => ValidationFailures.ThrowError(failure, statusCode);

    [DoesNotReturn]
    public void ThrowError(string message, int? statusCode = null)
        => ValidationFailures.ThrowError(message, statusCode);

    public void ThrowIfAnyErrors(int? statusCode = null)
        => ValidationFailures.ThrowIfAnyErrors(statusCode);
}

public class ValidationContext<T> : ValidationContext, IValidationErrors<T>
{
    public new static ValidationContext<T> Instance => new();

    /// <inheritdoc />
    public void AddError(Expression<Func<T, object?>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    /// <inheritdoc />
    [DoesNotReturn]
    public void ThrowError(Expression<Func<T, object?>> property, string errorMessage, int? statusCode = null)
        => ValidationFailures.ThrowError(property, errorMessage, statusCode);
}