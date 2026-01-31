using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Various DateTime formats and binding in AOT mode
public sealed class DateTimeBindingRequest
{
    public DateTime DateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
    public DateOnly DateOnly { get; set; }
    public TimeOnly TimeOnly { get; set; }
    public TimeSpan TimeSpan { get; set; }
    
    [QueryParam]
    public DateTime? QueryDateTime { get; set; }
    
    [QueryParam]
    public DateOnly? QueryDateOnly { get; set; }
}

public sealed class DateTimeBindingResponse
{
    public DateTime DateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
    public DateOnly DateOnly { get; set; }
    public TimeOnly TimeOnly { get; set; }
    public TimeSpan TimeSpan { get; set; }
    public DateTime? QueryDateTime { get; set; }
    public DateOnly? QueryDateOnly { get; set; }
    public bool AllDateTimesBound { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
}

public sealed class DateTimeBindingEndpoint : Endpoint<DateTimeBindingRequest, DateTimeBindingResponse>
{
    public override void Configure()
    {
        Post("datetime-binding-test");
        AllowAnonymous();
        SerializerContext<DateTimeBindingSerCtx>();
    }

    public override async Task HandleAsync(DateTimeBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DateTimeBindingResponse
        {
            DateTime = req.DateTime,
            DateTimeOffset = req.DateTimeOffset,
            DateOnly = req.DateOnly,
            TimeOnly = req.TimeOnly,
            TimeSpan = req.TimeSpan,
            QueryDateTime = req.QueryDateTime,
            QueryDateOnly = req.QueryDateOnly,
            AllDateTimesBound = req.DateTime != default && 
                                req.DateTimeOffset != default &&
                                req.DateOnly != default &&
                                req.TimeOnly != default,
            Year = req.DateTime.Year,
            Month = req.DateTime.Month,
            Day = req.DateTime.Day
        }, ct);
    }
}

[JsonSerializable(typeof(DateTimeBindingRequest))]
[JsonSerializable(typeof(DateTimeBindingResponse))]
public partial class DateTimeBindingSerCtx : JsonSerializerContext;
