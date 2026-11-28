namespace TestCases.ClientStreamingTest;

public class CurrentPosition
{
    public int Number { get; set; }

    /// <summary>
    /// optional id the handler reports to <see cref="StreamHandlerTracker" /> when it receives the first item and when it ends
    /// </summary>
    public string? TrackingId { get; set; }
}

public class ProgressReport
{
    public int LastNumber { get; set; }
}