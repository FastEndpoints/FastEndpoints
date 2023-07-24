namespace FastEndpoints;

public class JobQueueOptions
{
    //key: tCommand
    //val: handler execution time limit
    private readonly Dictionary<Type, (int concurrency, TimeSpan timeLimit)> _execLimits = new();

    public void ExecutionLimits<TCommand>(int maxConcurrency, TimeSpan timeLimit) where TCommand : ICommand
    {
        _execLimits[typeof(TCommand)] = new(maxConcurrency, timeLimit);
    }

    internal void SetExecutionLimits(Type tCommand, JobQueueBase jobQueue)
    {
        if (_execLimits.TryGetValue(tCommand, out var limits))
            jobQueue.SetExecutionLimits(limits.concurrency, limits.timeLimit);
    }
}