using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace FastEndpoints;

/// <summary>
/// RFC7807 compatible problem details/ error response class. this can be used by configuring startup like so:
/// <para>
/// <c>app.UseFastEndpoints(x => x.Errors.ResponseBuilder = ProblemDetails.ResponseBuilder);</c>
/// </para>
/// </summary>
public sealed class ProblemDetails
{
    /// <summary>
    /// the built-in function for transforming validation errors to a RFC7807 compatible problem details error response dto.
    /// </summary>
    public static Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { get; } = (failures, ctx, statusCode)
        => new ProblemDetails(failures, ctx.Request.Path, ctx.TraceIdentifier, statusCode);

    /// <summary>
    /// controls whether duplicate errors with the same name should be allowed.
    /// </summary>
    public static bool AllowDuplicates { private get; set; }

    /// <summary>
    /// globally sets the 'Type' value of the problem details dto.
    /// </summary>
    public static string TypeValue { private get; set; } = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";

    /// <summary>
    /// globally sets the 'Title' value of the problem details dto.
    /// </summary>
    public static string TitleValue { private get; set; } = "One or more validation errors occurred.";

#pragma warning disable CA1822
    public string Type => TypeValue;
    public string Title => TitleValue;
#pragma warning restore CA1822
    public int Status { get; init; }
    public string Instance { get; init; }
    public string TraceId { get; set; }
    public IEnumerable<Error> Errors { get; init; }

    public ProblemDetails(List<ValidationFailure> failures, string instance, string traceId, int statusCode)
    {
        Status = statusCode;
        Instance = instance;
        TraceId = traceId;

        if (AllowDuplicates)
        {
            Errors = failures.Select(f => new Error(f));
        }
        else
        {
            var set = new HashSet<Error>(failures.Count, Error.EqComparer);
            for (var i = 0; i < failures.Count; i++)
                set.Add(new Error(failures[i]));
            Errors = set;
        }
    }

    /// <summary>
    /// the error details object
    /// </summary>
    public sealed class Error
    {
        internal static Comparer EqComparer = new();

        /// <summary>
        /// the name of the error or property of the dto that caused the error
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// the reason for the error
        /// </summary>
        public string Reason { get; init; }

        /// <summary>
        /// the code of the error
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; init; }

        public Error(ValidationFailure failure)
        {
            Name = Config.SerOpts.Options.PropertyNamingPolicy?.ConvertName(failure.PropertyName) ?? failure.PropertyName;
            Reason = failure.ErrorMessage;
            Code = failure.ErrorCode;
        }

        internal sealed class Comparer : IEqualityComparer<Error>
        {
            public bool Equals(Error? x, Error? y) => x?.Name.Equals(y?.Name, StringComparison.OrdinalIgnoreCase) is true;
            public int GetHashCode([DisallowNull] Error obj) => obj.Name.GetHashCode();
        }
    }
}