using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace FastEndpoints;

/// <summary>
/// binder context supplied to custom request binders.
/// </summary>
public struct BinderContext
{
    /// <summary>
    /// the http context of the current request
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// a list of validation failures for the endpoint. you can add your own validation failures for properties of the request dto using this property.
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; init; }

    /// <summary>
    /// if the current endpoint is configured with a json serializer context, it will be provided to the custom request binder with this property.
    /// </summary>
    public JsonSerializerContext? JsonSerializerContext { get; init; }

    public BinderContext(
        HttpContext httpContext, List<ValidationFailure> validationFailures, JsonSerializerContext? jsonSerializerContext)
    {
        HttpContext = httpContext;
        ValidationFailures = validationFailures;
        JsonSerializerContext = jsonSerializerContext;
    }
}
