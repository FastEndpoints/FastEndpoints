using FastEndpoints.Validation.Results;

namespace FastEndpoints;

public class ErrorResponse
{
    public int StatusCode { get; set; } = 400;
    public string Message { get; set; } = "One or more errors occured!";
    public Dictionary<string, IEnumerable<string>> Errors { get; set; } = new();

    public ErrorResponse() { }

    public ErrorResponse(IEnumerable<ValidationFailure> failures)
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key,
                          g => g.Select(e => e.ErrorMessage));
    }
}

