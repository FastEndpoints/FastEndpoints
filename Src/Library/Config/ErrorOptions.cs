using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace FastEndpoints;

/// <summary>
/// error response customization settings
/// </summary>
public sealed class ErrorOptions
{
    /// <summary>
    /// this http status code will be used for all automatically sent validation failure responses. defaults to 400.
    /// </summary>
    public int StatusCode { internal get; set; } = 400;

    /// <summary>
    /// if this property is set, a <see cref="IProducesResponseTypeMetadata"/> will automatically be added to any endpoints that has a <see cref="Validator{TRequest}"/> associated with it.
    /// if you're not specifying your own <see cref="ResponseBuilder"/>, you'd typically be setting this to <c>typeOf(<see cref="ErrorResponse"/>)</c>
    /// </summary>
    public Type? ProducesMetadataType { internal get; set; }

    /// <summary>
    /// the general errors field name. this is the field name used for general errors when AddError() method is called without specifying a request dto property.
    /// </summary>
    public string GeneralErrorsField { internal get; set; } = "GeneralErrors";

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// the arguments for the func will be a list of validation failures, the http context and an http status code.
    /// </summary>
    public Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { internal get; set; }
        = (failures, _, statusCode) => new ErrorResponse(failures, statusCode);
}