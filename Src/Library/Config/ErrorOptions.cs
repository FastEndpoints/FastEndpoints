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
    /// the content-type header value for 400 error responses
    /// </summary>
    public string ContentType { internal get; set; } = "application/problem+json";

    /// <summary>
    /// if this property is not null, a <see cref="IProducesResponseTypeMetadata" /> will automatically be added to endpoints that has a
    /// <see cref="Validator{TRequest}" /> associated with it.
    /// if you're specifying your own <see cref="ResponseBuilder" />, don't forget to set this property to the correct type of the error response dto that
    /// your error response builder will be returning.
    /// <para>
    /// TIP: set this to null if you'd like to disable the auto adding of produces 400 metadata to endpoints even if they have validators associated with
    /// them.
    /// </para>
    /// </summary>
    public Type? ProducesMetadataType { internal get; set; } = typeof(ErrorResponse);

    /// <summary>
    /// the general errors field name. this is the field name used for general errors when AddError() method is called without specifying a request dto
    /// property.
    /// </summary>
    public string GeneralErrorsField { internal get; set; } = "GeneralErrors";

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// the arguments for the func will be a list of validation failures, the http context and an http status code.
    /// <para>
    /// HINT: if changing the default, make sure to also set <see cref="ProducesMetadataType" /> to the correct type of the error response dto.
    /// </para>
    /// </summary>
    public Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { internal get; set; }
        = (failures, _, statusCode) => new ErrorResponse(failures, statusCode);

    /// <summary>
    /// change the default error response builder to <see cref="ProblemDetails" /> instead of <see cref="ErrorResponse" />
    /// </summary>
    /// <param name="config">an action for configuring global settings for <see cref="ProblemDetails" /></param>
    public void UseProblemDetails(Action<ProblemDetailsConfig>? config = null)
    {
        ProducesMetadataType = typeof(ProblemDetails);
        ResponseBuilder = ProblemDetailsConf.ResponseBuilder;
        config?.Invoke(ProblemDetailsConf);
    }

    internal ProblemDetailsConfig ProblemDetailsConf { get; } = new();

    /// <summary>
    /// global settings for <see cref="ProblemDetails" /> error responses.
    /// </summary>
    public sealed class ProblemDetailsConfig
    {
        /// <summary>
        /// the built-in function for transforming validation errors to a RFC7807 compatible problem details error response dto.
        /// </summary>
        public Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { internal get; set; }
            = (failures, ctx, statusCode)
                  => new ProblemDetails(failures, ctx.Request.Path, ctx.TraceIdentifier, statusCode);

        /// <summary>
        /// globally sets the value of <see cref="ProblemDetails.Type" />.
        /// </summary>
        public string TypeValue { internal get; set; } = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";

        /// <summary>
        /// globally sets the value of <see cref="ProblemDetails.Title" />.
        /// </summary>
        public string TitleValue { internal get; set; } = "One or more validation errors occurred.";

        /// <summary>
        /// sets a function that will be called per instance/response that allows customization of the <see cref="ProblemDetails.Title" /> value.
        /// </summary>
        public Func<ProblemDetails, string> TitleTransformer { internal get; set; } = _ => Cfg.ErrOpts.ProblemDetailsConf.TitleValue;

        /// <summary>
        /// controls whether duplicate errors with the same name should be allowed for <see cref="ProblemDetails.Errors" />.
        /// </summary>
        public bool AllowDuplicateErrors { internal get; set; }

        /// <summary>
        /// if set to true, the <see cref="ValidationFailure.ErrorCode" /> value of the failure will be serialized to the response.
        /// </summary>
        public bool IndicateErrorCode { get; set; } = false;

        /// <summary>
        /// if set to true, the <see cref="FluentValidation.Severity" /> value of the failure will be serialized to the response.
        /// </summary>
        public bool IndicateErrorSeverity { get; set; } = false;
    }
}