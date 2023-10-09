using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Reflection;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.Http.Metadata;
#endif

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client
/// </summary>
public sealed class ErrorResponse : IResult
                                #if NET7_0_OR_GREATER
                                  , IEndpointMetadataProvider
#endif
{
    /// <summary>
    /// the http status code sent to the client. default is 400.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// the message for the error response
    /// </summary>
    public string Message { get; set; } = "One or more errors occurred!";

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
            f => Conf.SerOpts.Options.PropertyNamingPolicy?.ConvertName(f.PropertyName) ?? f.PropertyName,
            v => v.ErrorMessage);
    }

    public Task ExecuteAsync(HttpContext httpContext)
        => httpContext.Response.SendAsync(this, StatusCode);

#if NET7_0_OR_GREATER
    /// <inheritdoc />
    public static void PopulateMetadata(MethodInfo _, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(
            new ProducesResponseTypeMetadata
            {
                ContentTypes = new[] { "application/problem+json" },
                StatusCode = Config.ErrOpts.StatusCode,
                Type = typeof(ProblemDetails)
            });
    }
#endif
}