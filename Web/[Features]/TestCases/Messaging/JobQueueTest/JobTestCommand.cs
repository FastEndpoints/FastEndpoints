namespace TestCases.JobQueueTest;

public struct JobTestCommand : ICommand
{
    public static readonly List<int> CompletedIDs = [];

    public int Id { get; set; }

    public bool ShouldThrow { get; set; }
    public int ThrowCount { get; set; }
}

public struct JobTestCommandHandler : ICommandHandler<JobTestCommand>
{
    public Task ExecuteAsync(JobTestCommand cmd, CancellationToken ct)
    {
        if (cmd is { ShouldThrow: true, ThrowCount: 0 })
        {
            cmd.ThrowCount++;

            throw new InvalidOperationException();
        }

        JobTestCommand.CompletedIDs.Add(cmd.Id);

        return Task.CompletedTask;
    }
}