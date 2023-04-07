using FluentValidation.Results;

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// the http status code sent to the client. default is 400.
    /// </summary>
    public int StatusCode { get; set; }

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
    public ErrorResponse(List<ValidationFailure> failures, int statusCode = 400)
    {
        StatusCode = statusCode;
        Errors = failures.GroupToDictionary(
            f => Config.SerOpts.Options.PropertyNamingPolicy?.ConvertName(f.PropertyName) ?? f.PropertyName,
            v => v.ErrorMessage);
    }
}
