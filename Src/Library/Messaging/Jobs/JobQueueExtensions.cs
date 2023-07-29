using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
    /// enable job queue functionality with given settings
    /// </summary>
    /// <param name="options">specify settings/execution limits for each job queue type</param>
    /// <exception cref="InvalidOperationException">thrown when no commands/handlers have been detected</exception>
    public static IApplicationBuilder UseJobQueues(this IApplicationBuilder app, Action<JobQueueOptions>? options = null)
    {
        if (CommandExtensions.HandlerRegistry.Count == 0)
            throw new InvalidOperationException("No Commands/Handlers found in the system! Have you called AddFastEndpoints() yet?");

        var opts = new JobQueueOptions();
        options?.Invoke(opts);

        foreach (var tCommand in CommandExtensions.HandlerRegistry.Keys)
        {
            var tJobQ = typeof(JobQueue<,,>).MakeGenericType(tCommand, tStorageRecord, tStorageProvider);
            var jobQ = app.ApplicationServices.GetRequiredService(tJobQ);
            opts.SetExecutionLimits(tCommand, (JobQueueBase)jobQ);
        }

        return app;
    }

    /// <summary>
    /// queues up a given command in the respective job queue for that command type.
    /// </summary>
    /// <param name="cmd">the command to be queued</param>
    /// <param name="executeAfter">if set, the job won't be executed before this date/time. if unspecified, execution is attempted as soon as possible.</param>
    /// <param name="expireOn">if set, job will be considered stale/expired after this date/time. if unspecified, jobs expire after 4 hours of creation.</param>
    /// <param name="ct">cancellation token</param>
    public static Task QueueJobAsync(this ICommand cmd, DateTime? executeAfter = null, DateTime? expireOn = null, CancellationToken ct = default)
        => JobQueueBase.AddToQueueAsync(cmd, executeAfter, expireOn, ct);
}