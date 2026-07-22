using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

/// <summary>
/// extension methods for job queues
/// </summary>
public static class JobQueueExtensions
{
    const string AotWarning = "Reflection-based discovery is not supported. Use AddJobQueues<>(DiscoveredTypes.All) with the source generator.";

    static Type _tStorageRecord = null!;
    static Type _tStorageProvider = null!;

    /// <summary>
    /// add job queue functionality. when <c>.AddFastEndpoints()</c> is not in the pipeline, performs reflection-based type discovery of all loaded assemblies.
    /// </summary>
    /// <typeparam name="TStorageRecord">the implementation type of the job storage record</typeparam>
    /// <typeparam name="TStorageProvider">the implementation type of the job storage provider</typeparam>
    /// <param name="svc"></param>
    [UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
    public static IServiceCollection AddJobQueues<TStorageRecord, TStorageProvider>(this IServiceCollection svc)
        where TStorageRecord : class, IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        svc.AddMessaging();

        return AddJobQueuesCore<TStorageRecord, TStorageProvider>(svc);
    }

    /// <summary>
    /// add job queue functionality using reflection-based type discovery.
    /// </summary>
    /// <typeparam name="TStorageRecord">the implementation type of the job storage record</typeparam>
    /// <typeparam name="TStorageProvider">the implementation type of the job storage provider</typeparam>
    /// <param name="svc"></param>
    /// <param name="assemblies">
    /// assemblies to scan for command handlers, in addition to all loaded assemblies.
    /// only applicable when using job queues as a standalone library.
    /// </param>
    [RequiresUnreferencedCode(AotWarning), RequiresDynamicCode(AotWarning)]
    public static IServiceCollection AddJobQueues<TStorageRecord, TStorageProvider>(this IServiceCollection svc, params Assembly[]? assemblies)
        where TStorageRecord : class, IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        svc.AddMessaging(assemblies);

        return AddJobQueuesCore<TStorageRecord, TStorageProvider>(svc);
    }

    /// <summary>
    /// add job queue functionality using source-generated discovered types.
    /// pass one <see cref="List{Type}" /> per referenced assembly, e.g.:
    /// <c>AddJobQueues&lt;TStorageRecord, TStorageProvider&gt;(Lib1.DiscoveredTypes.All, Lib2.DiscoveredTypes.All)</c>
    /// <para>
    /// TIP: You don't need to pass discovered types here if you already called
    /// <c>.AddFastEndpoints(Lib1.DiscoveredTypes.All, ...)</c>. use <c>.AddJobQueues&lt;TStorageRecord, TStorageProvider&gt;()</c> instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TStorageRecord">the implementation type of the job storage record</typeparam>
    /// <typeparam name="TStorageProvider">the implementation type of the job storage provider</typeparam>
    /// <param name="svc"></param>
    /// <param name="discoveredTypes">one or more lists of source-generated discovered types, one per referenced assembly</param>
    public static IServiceCollection AddJobQueues<TStorageRecord, TStorageProvider>(this IServiceCollection svc, params List<Type>[] discoveredTypes)
        where TStorageRecord : class, IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        svc.AddMessaging(discoveredTypes);

        return AddJobQueuesCore<TStorageRecord, TStorageProvider>(svc);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2091")]
    static IServiceCollection AddJobQueuesCore<TStorageRecord, TStorageProvider>(IServiceCollection svc)
        where TStorageRecord : class, IJobStorageRecord, new()
        where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
    {
        _tStorageProvider = typeof(TStorageProvider);
        _tStorageRecord = typeof(TStorageRecord);

        if (_tStorageProvider.IsAssignableTo(Types.IJobResultProvider) &&
            !_tStorageRecord.IsAssignableTo(Types.IJobResultStorage))
            throw new InvalidOperationException($"Job storage record: [{typeof(TStorageRecord).FullName}] must implement [{nameof(IJobResultStorage)}]!");

        svc.AddSingleton<TStorageProvider>();
        svc.AddSingleton(typeof(IJobTracker<>), typeof(JobTracker<>));
        svc.AddSingleton(typeof(JobQueue<,,,>));

        return svc;
    }

    /// <summary>
    /// enable job queue functionality with given settings
    /// </summary>
    /// <param name="app"></param>
    /// <param name="options">specify settings/execution limits for each job queue type</param>
    /// <exception cref="InvalidOperationException">thrown when no commands/handlers have been detected</exception>
    public static IHost UseJobQueues(this IHost app, Action<JobQueueOptions>? options = null)
    {
        app.Services.UseJobQueues(options);

        return app;
    }

    /// <summary>
    /// enable job queue functionality with given settings
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="options">specify settings/execution limits for each job queue type</param>
    /// <exception cref="InvalidOperationException">thrown when no commands/handlers have been detected</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2075"), UnconditionalSuppressMessage("Trimming", "IL2055"), UnconditionalSuppressMessage("AOT", "IL3050")]
    public static IServiceProvider UseJobQueues(this IServiceProvider provider, Action<JobQueueOptions>? options = null)
    {
        provider.UseMessaging();

        var registry = provider.GetRequiredService<CommandHandlerRegistry>();

        if (registry.IsEmpty)
            throw new InvalidOperationException("No Commands/Handlers found in the system! Have you done the startup configuration correctly?");

        var opts = new JobQueueOptions();
        options?.Invoke(opts);

        if (opts.HasAnyIdempotencyConfig && !_tStorageRecord.IsAssignableTo(Types.IHasIdempotencyKey))
        {
            throw new InvalidOperationException(
                $"Job storage record: [{_tStorageRecord.FullName}] must implement [{nameof(IHasIdempotencyKey)}] when job idempotency is configured!");
        }

        foreach (var tCommand in registry.Keys.Where(t => t.IsAssignableTo(Types.ICommandBase)))
        {
            if (tCommand.ContainsGenericParameters)
                continue; //NOTE: no open generic command support for jobs.

            var tCommandInterface = tCommand.GetInterface(typeof(ICommand<>).Name);

            if (tCommandInterface is null)
                continue; //NOTE: IStreamCommand<> types are not supported as jobs.

            var tResult = tCommandInterface.GetGenericArguments()[0];

            var tHandler = provider.GetService(
                tResult == Types.VoidResult
                    ? Types.ICommandHandlerOf1.MakeGenericType(tCommand)
                    : Types.ICommandHandlerOf2.MakeGenericType(tCommand, tResult))?.GetType();

            if (tHandler is not null)
                registry[tCommand].HandlerType = tHandler;

            var tJobQ = Types.JobQueueOf4.MakeGenericType(tCommand, tResult, _tStorageRecord, _tStorageProvider);
            var jobQ = provider.GetRequiredService(tJobQ);
            opts.SetLimits(tCommand, (JobQueueBase)jobQ);
        }

        if (opts.WarmupRequested)
            MessagingExtensions.WarmupMessaging(provider);

        return provider;
    }

    /// <param name="cmd">the command to be set in the job</param>
    extension(ICommandBase cmd)
    {
        /// <summary>
        /// creates a new job object for the provided command.
        /// </summary>
        /// <typeparam name="TStorageRecord">the type of your <see cref="IJobStorageRecord" /> concrete class</typeparam>
        /// <param name="executeAfter">if set, the job won't be executed before this date/time. if unspecified, execution is attempted as soon as possible.</param>
        /// <param name="expireOn">if set, job will be considered stale/expired after this date/time. if unspecified, jobs expire after 4 hours of creation.</param>
        /// <returns>the new job object</returns>
        /// <exception cref="ArgumentException">
        /// thrown if the <paramref name="executeAfter" /> and <paramref name="expireOn" /> arguments are not UTC values, or if the effective expiration
        /// time is not later than the effective execution time
        /// </exception>
        public TStorageRecord CreateJob<TStorageRecord>(DateTime? executeAfter = null, DateTime? expireOn = null)
            where TStorageRecord : class, IJobStorageRecord, new()
            => JobQueueBase.CreateJob<TStorageRecord>(cmd, executeAfter, expireOn);

        /// <summary>
        /// triggers the execution of jobs in the respective queue for that command type.
        /// </summary>
        public void TriggerJobExecution()
            => JobQueueBase.TriggerJobExecution(cmd.GetType());
    }

    /// <summary>
    /// triggers the execution of jobs in the respective queue for that command type.
    /// </summary>
    /// <typeparam name="TCommand">the command type used to determine which queue to trigger</typeparam>
    public static void TriggerJobExecution<TCommand>() where TCommand : ICommandBase
        => JobQueueBase.TriggerJobExecution(typeof(TCommand));

    /// <summary>
    /// queues up a given command in the respective job queue for that command type.
    /// </summary>
    /// <param name="cmd">the command to be queued</param>
    /// <param name="executeAfter">if set, the job won't be executed before this date/time. if unspecified, execution is attempted as soon as possible.</param>
    /// <param name="expireOn">if set, job will be considered stale/expired after this date/time. if unspecified, jobs expire after 4 hours of creation.</param>
    /// <param name="ct">cancellation token</param>
    /// <exception cref="ArgumentException">
    /// thrown if the <paramref name="executeAfter" /> and <paramref name="expireOn" /> arguments are not UTC values, or if the effective expiration
    /// time is not later than the effective execution time
    /// </exception>
    public static Task<Guid> QueueJobAsync(this ICommandBase cmd, DateTime? executeAfter = null, DateTime? expireOn = null, CancellationToken ct = default)
        => JobQueueBase.AddToQueueAsync(cmd, executeAfter, expireOn, ct);
}