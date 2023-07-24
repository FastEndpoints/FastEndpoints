using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

public static class JobQueueExtensions
{
    private static Type tStorageRecord;
    private static Type tStorageProvider;

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

    public static Task QueueJobAsync(this ICommand cmd, CancellationToken ct = default)
        => JobQueueBase.AddToQueueAsync(cmd, ct);
}
