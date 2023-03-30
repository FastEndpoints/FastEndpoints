using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

internal interface IValidationErrors<T>
{
    /// <summary>
    /// validation failures collection for the endpoint
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// indicates if there are any validation failures for the current request
    /// </summary>
    bool ValidationFailed { get; }

    /// <summary>
    /// add a <see cref="ValidationFailure"/> to the current collection of validation failures of the endpoint
    /// </summary>
    /// <param name="failure">the validation failure to add</param>
    void AddError(ValidationFailure failure);

    /// <summary>
    /// adds a "GeneralError" to the current list of validation failures
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="errorCode">the error code associated with the error</param>
    /// <param name="severity">the severity of the error</param>
    void AddError(string message, string? errorCode = null, Severity severity = Severity.Error);

    /// <summary>
    /// adds an error message for the specified property of the request dto
    /// </summary>
    /// <param name="property">the property to add the error message for</param>
    /// <param name="errorMessage">the error message</param>
    /// <param name="errorCode">the error code associated with the error</param>
    /// <param name="severity">the severity of the error</param>
    void AddError(Expression<Func<T, object>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error);

    /// <summary>
    /// interrupt the flow of handler execution and send a 400 bad request with error details if there are any validation failures in the current request. if there are no validation failures, execution will continue past this call.
    /// </summary>
    void ThrowIfAnyErrors();

    /// <summary>
    /// adds a <see cref="ValidationFailure"/> to the validation failure collection of the endpoint and send back a 400 bad request with error details immediately interrupting handler execution flow. if there are any vallidation failures, no execution will continue past this call.
    /// </summary>
    /// <param name="failure">the validation failure to add</param>
    [DoesNotReturn]
    void ThrowError(ValidationFailure failure);

    /// <summary>
    /// add a "GeneralError" to the validation failure list and send back a 400 bad request with error details immediately interrupting handler execution flow. if there are any vallidation failures, no execution will continue past this call.
    /// </summary>
    /// <param name="message">the error message</param>
    [DoesNotReturn]
    void ThrowError(string message);

    /// <summary>
    /// adds an error message for the specified property of the request dto and sends back a 400 bad request with error details immediately interrupting handler execution flow. no execution will continue past this call.
    /// </summary>
    /// <param name="property">the property to add the error message for</param>
    /// <param name="errorMessage">the error message</param>
    [DoesNotReturn]
    void ThrowError(Expression<Func<T, object>> property, string errorMessage);
}
