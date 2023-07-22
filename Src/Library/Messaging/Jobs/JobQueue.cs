namespace FastEndpoints;

public class JobQueueBase
{

}

public class JobQueue<TCommand> : JobQueueBase where TCommand : ICommand
{
    private static readonly ParallelOptions parallelOpts = new() { MaxDegreeOfParallelism = 1 };
    private static readonly string jobQueueID = typeof(TCommand).FullName!;


}

public interface IJobRecord
{
    public string QueueID { get; set; }
    public object Command { get; set; }

}