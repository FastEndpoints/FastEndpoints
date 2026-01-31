using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request with DateOnly and TimeOnly types
public class DateOnlyTimeOnlyRequest
{
    public DateOnly BirthDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly? OptionalDate { get; set; }
    public TimeOnly? OptionalTime { get; set; }
    public List<DateOnly> ImportantDates { get; set; } = [];
}

public class DateOnlyTimeOnlyResponse
{
    public DateOnly BirthDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DayOfYear { get; set; }
    public int Hour { get; set; }
    public int ImportantDatesCount { get; set; }
    public bool DateOnlyWorked { get; set; }
    public bool TimeOnlyWorked { get; set; }
}

/// <summary>
/// Tests DateOnly and TimeOnly types in AOT mode.
/// AOT ISSUE: DateOnly/TimeOnly are newer types needing explicit converter support.
/// JSON serialization of these types requires specific converters.
/// Nullable variants need additional type handling.
/// </summary>
public class DateOnlyTimeOnlyEndpoint : Endpoint<DateOnlyTimeOnlyRequest, DateOnlyTimeOnlyResponse>
{
    public override void Configure()
    {
        Post("date-time-only-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DateOnlyTimeOnlyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DateOnlyTimeOnlyResponse
        {
            BirthDate = req.BirthDate,
            StartTime = req.StartTime,
            DayOfYear = req.BirthDate.DayOfYear,
            Hour = req.StartTime.Hour,
            ImportantDatesCount = req.ImportantDates.Count,
            DateOnlyWorked = req.BirthDate != default,
            TimeOnlyWorked = req.StartTime != default
        });
    }
}
