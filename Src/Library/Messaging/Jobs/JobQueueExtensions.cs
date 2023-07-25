using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

/// <summary>
/// extension methods for job queues
/// </summary>
public static class JobQueueExtensions
{
    private static Type tStorageRecord;
    private static Type tStorageProvider;

    /// <summary>
    /// add job queue functionality
    /// </summary>
    /// <typeparam name="TStorageRecord">the implementation type of the job storage record</typeparam>
    /// <typeparam name="TStorageProvider">the implementation type of the job storage provider</typeparam>
    public static IServiceCollection AddJobQueues<TStorageRecord, TStorageProvider>(this IServiceCollection svc)
        where TStorageRecord : IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        tStorageProvider = typeof(TStorageProvider);
        tStorageRecord = typeof(TStorageRecord);
        svc.AddSingleton<TStorageProvider>();
        svc.AddSingleton(typeof(JobQueue<,,>));
        return svc;
    }

    /// <summary>
    /// enable job queue functionality with give settings
    /// </summary>
    /// <param name="options">specify settings/execution limits for each job queue type</param>
    /// <exception cref="InvalidOperationException">thrown when no commands/handlers have been detected</exception>
    public static IHost UseJobQueues(this IHost host, Action<JobQueueOptions> options)
    {
        if (CommandExtensions.handlerRegistry.Count == 0)
            throw new InvalidOperationException("No Commands/Handlers found in the system!");

        var opts = new JobQueueOptions();
        options(opts);

        foreach (var tCommand in CommandExtensions.handlerRegistry.Keys)
        {
            var tJobQ = typeof(JobQueue<,,>).MakeGenericType(tCommand, tStorageRecord, tStorageProvider);
            var jobQ = host.Services.GetRequiredService(tJobQ);
            opts.SetExecutionLimits(tCommand, (JobQueueBase)jobQ);
        }

        return host;
    }

    /// <summary>
    /// queues up a given command in the respective job queue for that command type.
    /// </summary>
    /// <param name="cmd">the command to be queued</param>
    /// <param name="ct">cancellation token</param>
    public static Task QueueJobAsync(this ICommand cmd, CancellationToken ct = default)
        => JobQueueBase.AddToQueueAsync(cmd, ct);
}