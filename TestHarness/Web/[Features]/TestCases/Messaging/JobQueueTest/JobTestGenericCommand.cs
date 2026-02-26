namespace TestCases.JobQueueTest;

public class JobTestGenericCommand<TEvent> : ICommand
    where TEvent : IEvent
{
    public static readonly List<int> CompletedIDs = [];

    public int Id { get; set; }
    public TEvent Event { get; set; }
}

public class JobTestGenericCommandHandler<TEvent> : ICommandHandler<JobTestGenericCommand<TEvent>>
    where TEvent : IEvent
{
    public Task ExecuteAsync(JobTestGenericCommand<TEvent> cmd, CancellationToken ct)
    {
        JobTestGenericCommand<TEvent>.CompletedIDs.Add(cmd.Id);

        return Task.CompletedTask;
    }
}