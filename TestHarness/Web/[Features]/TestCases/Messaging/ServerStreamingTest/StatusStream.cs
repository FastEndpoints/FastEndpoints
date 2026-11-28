namespace TestCases.ServerStreamingTest;

public class StatusStreamCommand : IServerStreamCommand<StatusUpdate>
{
    public int Id { get; set; }

    /// <summary>
    /// number of updates to send before ending the stream. 0 keeps streaming until cancelled.
    /// </summary>
    public int Count { get; set; }
}

public class StatusUpdate
{
    public string Message { get; set; }
}