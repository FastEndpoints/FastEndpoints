using System.Collections.Concurrent;

namespace TestCases;

/// <summary>
/// lets rpc tests observe when a server-side stream handler starts and ends. works because the test host runs in the same process.
/// </summary>
public static class StreamHandlerTracker
{
    static readonly ConcurrentDictionary<string, (TaskCompletionSource Started, TaskCompletionSource Ended)> _runs = new();

    public static void Started(string runId)
        => Get(runId).Started.TrySetResult();

    public static void Ended(string runId)
        => Get(runId).Ended.TrySetResult();

    public static Task WhenStarted(string runId)
        => Get(runId).Started.Task;

    public static Task WhenEnded(string runId)
        => Get(runId).Ended.Task;

    static (TaskCompletionSource Started, TaskCompletionSource Ended) Get(string runId)
        => _runs.GetOrAdd(
            runId,
            static _ => (new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)));
}
