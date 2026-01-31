using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request with time-related operations
public class TimeProviderRequest
{
    public string TimeZoneId { get; set; } = string.Empty;
    public DateTime UtcDateTime { get; set; }
    public DateTimeOffset DateTimeOffset { get; set; }
}

public class TimeProviderResponse
{
    public DateTime LocalTime { get; set; }
    public string TimeZoneName { get; set; } = string.Empty;
    public TimeSpan UtcOffset { get; set; }
    public bool IsDaylightSavingTime { get; set; }
    public bool TimeProviderWorked { get; set; }
}

/// <summary>
/// Tests TimeZoneInfo and time conversions in AOT mode.
/// AOT ISSUE: TimeZoneInfo.FindSystemTimeZoneById uses registry/file lookup.
/// Time zone database access may need runtime resources.
/// DateTimeOffset conversions use time zone metadata.
/// </summary>
public class TimeProviderEndpoint : Endpoint<TimeProviderRequest, TimeProviderResponse>
{
    public override void Configure()
    {
        Post("time-provider-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TimeProviderRequest req, CancellationToken ct)
    {
        TimeZoneInfo? timeZone = null;
        DateTime localTime = req.UtcDateTime;
        string timeZoneName = "UTC";
        TimeSpan utcOffset = TimeSpan.Zero;
        bool isDst = false;

        try
        {
            if (!string.IsNullOrEmpty(req.TimeZoneId))
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(req.TimeZoneId);
                localTime = TimeZoneInfo.ConvertTimeFromUtc(req.UtcDateTime, timeZone);
                timeZoneName = timeZone.DisplayName;
                utcOffset = timeZone.GetUtcOffset(req.UtcDateTime);
                isDst = timeZone.IsDaylightSavingTime(req.UtcDateTime);
            }
        }
        catch (TimeZoneNotFoundException)
        {
            timeZoneName = "Unknown TimeZone";
        }

        await Send.OkAsync(new TimeProviderResponse
        {
            LocalTime = localTime,
            TimeZoneName = timeZoneName,
            UtcOffset = utcOffset,
            IsDaylightSavingTime = isDst,
            TimeProviderWorked = timeZone != null
        });
    }
}
