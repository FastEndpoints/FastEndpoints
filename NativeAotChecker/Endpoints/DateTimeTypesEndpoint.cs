namespace NativeAotChecker.Endpoints;

// Test DateTime/DateOnly/TimeOnly binding - likely AOT issue
public sealed class DateTimeTypesRequest
{
    public DateTime DateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
    public DateOnly DateOnly { get; set; }
    public TimeOnly TimeOnly { get; set; }
    public TimeSpan TimeSpan { get; set; }
    
    [QueryParam]
    public DateTime? QueryDateTime { get; set; }
}

public sealed class DateTimeTypesResponse
{
    public DateTime DateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
    public DateOnly DateOnly { get; set; }
    public TimeOnly TimeOnly { get; set; }
    public TimeSpan TimeSpan { get; set; }
    public DateTime? QueryDateTime { get; set; }
    public string DateOnlyFormatted { get; set; } = "";
}

public sealed class DateTimeTypesEndpoint : Endpoint<DateTimeTypesRequest, DateTimeTypesResponse>
{
    public override void Configure()
    {
        Post("datetime-types-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DateTimeTypesRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DateTimeTypesResponse
        {
            DateTime = req.DateTime,
            DateTimeOffset = req.DateTimeOffset,
            DateOnly = req.DateOnly,
            TimeOnly = req.TimeOnly,
            TimeSpan = req.TimeSpan,
            QueryDateTime = req.QueryDateTime,
            DateOnlyFormatted = req.DateOnly.ToString("yyyy-MM-dd")
        });
    }
}
