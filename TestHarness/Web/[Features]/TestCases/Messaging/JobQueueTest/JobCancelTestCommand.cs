namespace TestCases.JobQueueTest;

public class JobCancelTestCommand : ICommand
{
    public Guid TrackingId { get; set; }
    public int Counter { get; set; }
    public bool IsCancelled { get; set; }
}

public class JobCancelTestCommandHandler : ICommandHandler<JobCancelTestCommand>
{
    public async Task ExecuteAsync(JobCancelTestCommand cmd, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            cmd.Counter++;
            await Task.Delay(100);
        }
        cmd.IsCancelled = true;
    }
}