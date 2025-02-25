using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// extension methods for job queues
/// </summary>
public static class JobQueueExtensions
{
    static Type _tStorageRecord = null!;
    static Type _tStorageProvider = null!;

    /// <summary>
    /// add job queue functionality
    /// </summary>
    /// <typeparam name="TStorageRecord">the implementation type of the job storage record</typeparam>
    /// <typeparam name="TStorageProvider">the implementation type of the job storage provider</typeparam>
    public static IServiceCollection AddJobQueues<TStorageRecord, TStorageProvider>(this IServiceCollection svc)
        where TStorageRecord : class, IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        _tStorageProvider = typeof(TStorageProvider);
        _tStorageRecord = typeof(TStorageRecord);

        if (_tStorageProvider.IsAssignableTo(Types.IJobResultProvider) &&
            !_tStorageRecord.IsAssignableTo(Types.IJobResultStorage))
            throw new InvalidOperationException($"Job storage record: [{typeof(TStorageRecord).FullName}] must implement [{nameof(IJobResultProvider)}]!");

        svc.AddSingleton<TStorageProvider>();
        svc.AddSingleton(typeof(IJobTracker<>), typeof(JobTracker<>));
        svc.AddSingleton(typeof(JobQueue<,,,>));

        return svc;
    }

    /// <summary>
    /// enable job queue functionality with given settings
    /// </summary>
    /// <param name="options">specify settings/execution limits for each job queue type</param>
    /// <exception cref="InvalidOperationException">thrown when no commands/handlers have been detected</exception>
    public static IApplicationBuilder UseJobQueues(this IApplicationBuilder app, Action<JobQueueOptions>? options = null)
    {
        var registry = app.ApplicationServices.GetRequiredService<CommandHandlerRegistry>();

        if (registry.IsEmpty)
            throw new InvalidOperationException("No Commands/Handlers found in the system! Have you called yet AddFastEndpoints() on the IServiceCollection, or RegisterGenericCommand previously on the WebApplication?");

        var opts = new JobQueueOptions();
        options?.Invoke(opts);

        foreach (var tCommand in registry.Keys.Where(t => t.IsAssignableTo(Types.ICommandBase)))
        {
            if (tCommand.ContainsGenericParameters)
                continue; //NOTE: no open generic command support for jobs.

            var tResult = tCommand.GetInterface(typeof(ICommand<>).Name)!.GetGenericArguments()[0];

            var tHandler = app.ApplicationServices.GetService(
                tResult == Types.VoidResult
                    ? Types.ICommandHandlerOf1.MakeGenericType(tCommand)
                    : Types.ICommandHandlerOf2.MakeGenericType(tCommand, tResult))?.GetType();

            if (tHandler is not null)
                registry[tCommand].HandlerType = tHandler;

            var tJobQ = Types.JobQueueOf4.MakeGenericType(tCommand, tResult, _tStorageRecord, _tStorageProvider);
            var jobQ = app.ApplicationServices.GetRequiredService(tJobQ);
            opts.SetLimits(tCommand, (JobQueueBase)jobQ);
        }

        return app;
    }

    /// <summary>
    /// creates a new job object for the provided command.
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of your <see cref="IJobStorageRecord" /> concrete class</typeparam>
    /// <param name="cmd">the command to be set in the job</param>
    /// <param name="executeAfter">if set, the job won't be executed before this date/time. if unspecified, execution is attempted as soon as possible.</param>
    /// <param name="expireOn">if set, job will be considered stale/expired after this date/time. if unspecified, jobs expire after 4 hours of creation.</param>
    /// <returns>the new job object</returns>
    /// <exception cref="ArgumentException">thrown if the <paramref name="executeAfter" /> and <paramref name="expireOn" /> arguments are not UTC values</exception>
    public static TStorageRecord CreateJob<TStorageRecord>(this ICommandBase cmd, DateTime? executeAfter = null, DateTime? expireOn = null)
        where TStorageRecord : class, IJobStorageRecord, new()
        => JobQueueBase.CreateJob<TStorageRecord>(cmd, executeAfter, expireOn);

    /// <summary>
    /// triggers the execution of jobs in the respective queue for that command type.
    /// </summary>
    /// <param name="cmd">the command used to determine which queue to trigger</param>
    public static void TriggerJobExecution(this ICommandBase cmd)
        => JobQueueBase.TriggerJobExecution(cmd);

    /// <summary>
    /// queues up a given command in the respective job queue for that command type.
    /// </summary>
    /// <param name="cmd">the command to be queued</param>
    /// <param name="executeAfter">if set, the job won't be executed before this date/time. if unspecified, execution is attempted as soon as possible.</param>
    /// <param name="expireOn">if set, job will be considered stale/expired after this date/time. if unspecified, jobs expire after 4 hours of creation.</param>
    /// <param name="ct">cancellation token</param>
    /// <exception cref="ArgumentException">thrown if the <paramref name="executeAfter" /> and <paramref name="expireOn" /> arguments are not UTC values</exception>
    public static Task<Guid> QueueJobAsync(this ICommandBase cmd, DateTime? executeAfter = null, DateTime? expireOn = null, CancellationToken ct = default)
        => JobQueueBase.AddToQueueAsync(cmd, executeAfter, expireOn, ct);
}