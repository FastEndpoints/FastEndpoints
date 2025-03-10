using System.ComponentModel;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client
/// </summary>
public sealed class ErrorResponse : IResult, IEndpointMetadataProvider
{
    /// <summary>
    /// the http status code sent to the client. default is 400.
    /// </summary>
    [DefaultValue(400)]
    public int StatusCode { get; set; }

    /// <summary>
    /// the message for the error response
    /// </summary>
    [DefaultValue("One or more errors occurred!")]
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
    /// <param name="failures">validation failures to initialize the DTO with</param>
    public ErrorResponse(List<ValidationFailure> failures, int statusCode = 400)
    {
        StatusCode = statusCode;
        Errors = failures.GroupToDictionary(
            f => Cfg.SerOpts.Options.PropertyNamingPolicy?.ConvertName(f.PropertyName) ?? f.PropertyName,
            v => v.ErrorMessage);
    }

    public Task ExecuteAsync(HttpContext httpContext)
        => httpContext.Response.SendAsync(this, StatusCode);

    static readonly string[] _contentTypes = [Cfg.ErrOpts.ContentType];

    /// <inheritdoc />
    public static void PopulateMetadata(MethodInfo _, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(
            new ProducesResponseTypeMetadata
            {
                ContentTypes = _contentTypes,
                StatusCode = Cfg.ErrOpts.StatusCode,
                Type = typeof(ProblemDetails)
            });
    }
}