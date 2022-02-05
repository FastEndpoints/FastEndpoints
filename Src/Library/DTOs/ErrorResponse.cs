using FastEndpoints.Validation;

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// the http status code sent to the client. default is 400.
    /// </summary>
    public int StatusCode { get; set; } = 400;

    /// <summary>
    /// the message for the error response
    /// </summary>
    public string Message { get; set; } = "One or more errors occured!";

    /// <summary>
    /// the collection of errors for the current context
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = new();

    /// <summary>
    /// instantiate a new error response without any errors
    /// </summary>
    public ErrorResponse() { }

    /// <summary>
    /// instantiate an error response with the given collection validation failures
    /// </summary>
    /// <param name="failures"></param>
    public ErrorResponse(List<ValidationFailure> failures)
    {
        Errors = failures.GroupToDictionary(f => f.PropertyName, v => v.ErrorMessage);
    }
}