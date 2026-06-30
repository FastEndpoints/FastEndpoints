using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// provides a way to manipulate the validation failures of the current endpoint context.
/// call <see cref="Instance" /> to obtain an instance of the current validation context.
/// </summary>
public class ValidationContext : IValidationErrors
{
    static readonly IHttpContextAccessor? _httpCtxAccessor = ServiceResolver.Instance.Resolve<IHttpContextAccessor>();

    /// <summary>
    /// provides access to the validation context of the currently executing endpoint.
    /// </summary>
    public static ValidationContext Instance => new();

    /// <summary>
    /// validation failures collection for the endpoint
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; } = (List<ValidationFailure>?)_httpCtxAccessor?.HttpContext?.Items[CtxKey.ValidationFailures] ?? [];

    /// <summary>
    /// indicates if there are any validation failures for the current request
    /// </summary>
    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    /// <summary>
    /// add a <see cref="ValidationFailure" /> to the current collection of validation failures of the endpoint
    /// </summary>
    /// <param name="failure">the validation failure to add</param>
    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure);

    /// <summary>
    /// adds a "GeneralError" to the current list of validation failures
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="errorCode">the error code associated with the error</param>
    /// <param name="severity">the severity of the error</param>
    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    /// <summary>
    /// adds a <see cref="ValidationFailure" /> to the validation failure collection of the endpoint and send back a 400 bad request with error details
    /// immediately interrupting handler execution flow. i.e. execution will not continue past this call.
    /// </summary>
    /// <param name="failure">the validation failure to add</param>
    /// <param name="statusCode">an optional status code to be used when building the error response</param>
    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure, int? statusCode = null)
        => ValidationFailures.ThrowError(failure, statusCode);

    /// <summary>
    /// adds a "GeneralError" to the validation failure list and sends back a 400 bad request with error details immediately interrupting handler execution
    /// flow. i.e. execution will not continue past this call.
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="statusCode">an optional status code to be used when building the error response</param>
    [DoesNotReturn]
    public void ThrowError(string message, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message);

    /// <summary>
    /// adds a "GeneralError" to the validation failure list and sends back a 400 bad request with error details immediately interrupting handler execution
    /// flow. i.e. execution will not continue past this call.
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="errorCode">the error code associated with the error</param>
    /// <param name="severity">the severity of the error</param>
    /// <param name="statusCode">an optional status code to be used when building the error response</param>
    [DoesNotReturn]
    public void ThrowError(string message, string errorCode, Severity severity = Severity.Error, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message, errorCode, severity);

    /// <summary>
    /// interrupt the flow of handler execution and send a 400 bad request with error details if there are any validation failures in the current request. if
    /// there are no validation failures, execution will continue past this call.
    /// </summary>
    /// <param name="statusCode">an optional status code to be used when building the error response</param>
    public void ThrowIfAnyErrors(int? statusCode = null)
        => ValidationFailures.ThrowIfAnyErrors(statusCode);
}

public class ValidationContext<T> : ValidationContext, IValidationErrors<T>
{
    public new static ValidationContext<T> Instance => new();

    public void AddError(Expression<Func<T, object?>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<T, object?>> property, string errorMessage, int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<T, object?>> property,
                           string errorMessage,
                           string errorCode,
                           Severity severity = Severity.Error,
                           int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage, errorCode, severity);
}