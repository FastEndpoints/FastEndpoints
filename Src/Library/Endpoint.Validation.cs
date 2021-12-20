using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using System.Linq.Expressions;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// adds a "GeneralError" to the current list of validation failures
    /// </summary>
    /// <param name="message">the error message</param>
    protected void AddError(string message)
        => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

    /// <summary>
    /// adds an error message for the specified property of the request dto
    /// </summary>
    /// <param name="property">the property to add teh error message for</param>
    /// <param name="errorMessage">the error message</param>
    protected void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
    {
        ValidationFailures.Add(
            new ValidationFailure(property.PropertyName(), errorMessage));
    }

    /// <summary>
    /// interrupt the flow of handler execution and send a 400 bad request with error details if there are any validation failures in the current request. if there are no validation failures, execution will continue past this call.
    /// </summary>
    protected void ThrowIfAnyErrors()
    {
        if (ValidationFailed) throw new ValidationFailureException();
    }

    /// <summary>
    /// add a "GeneralError" to the validation failure list and send back a 400 bad request with error details immediately interrupting handler execution flow. if there are any vallidation failures, no execution will continue past this call.
    /// </summary>
    /// <param name="message">the error message</param>
    protected void ThrowError(string message)
    {
        AddError(message);
        ThrowIfAnyErrors();
    }

    /// <summary>
    /// adds an error message for the specified property of the request dto and sends back a 400 bad request with error details immediately interrupting handler execution flow. no execution will continue past this call.
    /// </summary>
    /// <param name="property"></param>
    /// <param name="errorMessage"></param>
    protected void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
    {
        AddError(property, errorMessage);
        ThrowIfAnyErrors();
    }
}

