using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// RFC7807 compatible problem details/ error response class. this can be used by configuring startup like so:
/// <para>
/// <c>app.UseFastEndpoints(x => x.Errors.ResponseBuilder = ProblemDetails.ResponseBuilder);</c>
/// </para>
/// </summary>
public sealed class ProblemDetails
{
    public static Func<List<ValidationFailure>, HttpContext, int, object> ResponseBuilder { get; } = (failures, ctx, statusCode)
        => new ProblemDetails(failures, ctx.Request.Path, ctx.TraceIdentifier, statusCode);

    public string Type { get; } = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";
    public string Title { get; } = "One or more validation errors occurred.";
    public int Status { get; init; }
    public string Instance { get; init; }
    public string TraceId { get; set; }
    public IEnumerable<Error> Errors { get; init; }

    public class Error
    {
        public string Name { get; init; }
        public string Reason { get; init; }
        public string? Code { get; init; }

        public Error(string name, string reason, string? code)
        {
            Name = name;
            Reason = reason;
            Code = code;
        }
    }

    public ProblemDetails() { }

    public ProblemDetails(List<ValidationFailure> failures, string instance, string traceId, int statusCode)
    {
        Status = statusCode;
        Instance = instance;
        TraceId = traceId;
        Errors = failures
            .GroupToDictionary(
                f => f.PropertyName,
                v => new Error(Config.SerOpts.Options.PropertyNamingPolicy?.ConvertName(v.PropertyName) ?? v.PropertyName, v.ErrorMessage, v.ErrorCode))
            .Select(kvp => kvp.Value[0]);
    }
}