using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;
using System.ComponentModel;

#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Builder;
using System.Reflection;
#endif

namespace FastEndpoints;

/// <summary>
/// RFC7807 compatible problem details/ error response class. this can be used by configuring startup like so:
/// <para>
///     <c>app.UseFastEndpoints(x => x.Errors.ResponseBuilder = ProblemDetails.ResponseBuilder);</c>
/// </para>
/// </summary>

#if NET7_0_OR_GREATER
public sealed class ProblemDetails : IResult, IEndpointMetadataProvider
#else
public sealed class ProblemDetails : IResult
#endif
{
#pragma warning disable CA1822
    [DefaultValue("https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1")]
    public string Type => Cfg.ErrOpts.ProblemDetailsConf.TypeValue;

    [DefaultValue("One or more validation errors occurred.")]
    public string Title => Cfg.ErrOpts.ProblemDetailsConf.TitleTransformer(this);
#pragma warning restore CA1822

    [DefaultValue(400)]
    public int Status { get; set; }

    [DefaultValue("/api/route")]
    public string Instance { get; set; } = null!;

    [DefaultValue("0HMPNHL0JHL76:00000001")]
    public string TraceId { get; set; } = null!;

    /// <summary>
    /// the details of the error
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    public IEnumerable<Error> Errors { get; set; } = null!;

    public ProblemDetails() { }

    public ProblemDetails(IReadOnlyList<ValidationFailure> failures, int? statusCode = null)
    {
        Initialize(failures, null!, null!, statusCode ?? Cfg.ErrOpts.StatusCode);
    }

    public ProblemDetails(IReadOnlyList<ValidationFailure> failures, string instance, string traceId, int statusCode)
    {
        Initialize(failures, instance, traceId, statusCode);
    }

    void Initialize(IReadOnlyList<ValidationFailure> failures, string instance, string traceId, int statusCode)
    {
        Status = statusCode;
        Instance = instance;
        TraceId = traceId;

        if (Cfg.ErrOpts.ProblemDetailsConf.AllowDuplicateErrors)
            Errors = failures.Select(f => new Error(f));
        else
        {
            var set = new HashSet<Error>(failures.Count, Error.EqComparer);
            for (var i = 0; i < failures.Count; i++)
                set.Add(new(failures[i]));
            Errors = set;
        }
    }

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (string.IsNullOrEmpty(TraceId))
            TraceId = httpContext.TraceIdentifier;
        if (string.IsNullOrEmpty(Instance))
            Instance = httpContext.Request.Path;

        return httpContext.Response.SendAsync(this, Status);
    }

#if NET7_0_OR_GREATER
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
#endif

    /// <summary>
    /// the error details object
    /// </summary>
    public sealed class Error
    {
        internal static readonly Comparer EqComparer = new();

        /// <summary>
        /// the name of the error or property of the dto that caused the error
        /// </summary>
        [DefaultValue("Error or field name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// the reason for the error
        /// </summary>
        [DefaultValue("Error reason")]
        public string Reason { get; set; } = null!;

        /// <summary>
        /// the code of the error
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; set; }

        /// <summary>
        /// the severity of the error
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Severity { get; set; }

        public Error() { }

        public Error(ValidationFailure failure)
        {
            Name = Cfg.SerOpts.Options.PropertyNamingPolicy?.ConvertName(failure.PropertyName) ?? failure.PropertyName;
            Reason = failure.ErrorMessage;
            Code = Cfg.ErrOpts.ProblemDetailsConf.IndicateErrorCode ? failure.ErrorCode : null;
            Severity = Cfg.ErrOpts.ProblemDetailsConf.IndicateErrorSeverity ? failure.Severity.ToString() : null;
        }

        internal sealed class Comparer : IEqualityComparer<Error>
        {
            public bool Equals(Error? x, Error? y)
                => x?.Name.Equals(y?.Name, StringComparison.OrdinalIgnoreCase) is true;

            public int GetHashCode(Error obj)
                => obj.Name.GetHashCode();
        }
    }
}