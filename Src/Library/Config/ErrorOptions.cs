using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// error response customization settings
/// </summary>
public class ErrorOptions
{
    /// <summary>
    /// this http status code will be used for all automatically sent validation failure responses. defaults to 400.
    /// </summary>
    public int StatusCode { internal get; set; } = 400;

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// the arguments for the func will be a list of validation failures, the http context and an http status code.
    /// </summary>
    public Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { internal get; set; }
        = (failures, _, statusCode) => new ErrorResponse(failures, statusCode);
}