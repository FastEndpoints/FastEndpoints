using System.Diagnostics;
using System.Runtime.CompilerServices;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;

namespace MessagingAotChecker;

public static class Program
{
    public static async Task Main()
    {
        foreach (var explicitHandlers in new[] { false, true })
        {
            foreach (var warmup in new[] { false, true })
            {
                var services = new ServiceCollection().AddMessaging(DiscoveredTypes.All);

                if (explicitHandlers)
                {
                    services.AddTransient<IEventHandler<BaseEvent>, DualHandler>();
                    services.AddTransient<IEventHandler<DerivedEvent>, DualHandler>();
                    services.AddTransient<IEventHandler<IContract>, InterfaceHandler>();
                }
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

                // Resolve before configuration to exercise lazy mode selection and shared contract instances.
                var bus = provider.GetRequiredService<EventBus<BaseEvent>>();
                provider.UseMessaging(o => o.Events.EnableInheritance = true);
                if (warmup)
                    provider.UseMessaging(o => o.Warmup());
                provider.UseMessaging();
                var model = new DerivedEvent();
                await bus.PublishAsync(model);
                Check(model.BaseCalls == 1 && model.DerivedCalls == 1 && model.InterfaceCalls == 1, "exact subscribed methods");
                IEvent asEvent = model;
                await asEvent.PublishAsync();
                Check(model.BaseCalls == 2 && model.DerivedCalls == 2 && model.InterfaceCalls == 2, "runtime publishing bridge");
                await model.PublishAsync();
                Check(model.BaseCalls == 3 && model.DerivedCalls == 3 && model.InterfaceCalls == 3, "generic publishing");
                var decisions = 0;
                await bus.PublishFilteredAsync(
                    model,
                    t =>
                    {
                        decisions++;

                        return t == typeof(DualHandler);
                    });
                Check(decisions == 2 && model.BaseCalls == 4 && model.DerivedCalls == 4 && model.InterfaceCalls == 3, "filter by implementation");

                var interfaceOnly = new InterfaceOnlyEvent();
                await interfaceOnly.PublishAsync();
                Check(interfaceOnly.InterfaceCalls == 1, "interface-only subscription");
                var failure = new InvalidOperationException("original handler failure");

                try
                {
                    await provider.GetRequiredService<EventBus<DerivedThrowEvent>>().PublishAsync(new(failure));

                    throw new InvalidOperationException("missing handler exception");
                }
                catch (InvalidOperationException caught) when (ReferenceEquals(caught, failure)) { }

                var single = provider.GetRequiredService<EventBus<InterfaceOnlyEvent>>();
                Measure("inherited-single", () => single.PublishAsync(interfaceOnly));
                Measure("inherited-many", () => bus.PublishAsync(model));
                var empty = provider.GetRequiredService<EventBus<EmptyEvent>>();
                var emptyModel = new EmptyEvent();
                Measure("inherited-empty", () => empty.PublishAsync(emptyModel));
            }
        }

        using (var exact = new ServiceCollection().AddMessaging(DiscoveredTypes.All).BuildServiceProvider())
        {
            exact.UseMessaging();
            var bus = exact.GetRequiredService<EventBus<BaseEvent>>();
            var model = new DerivedEvent();
            await bus.PublishAsync(model);
            Check(model.BaseCalls == 1 && model.DerivedCalls == 0 && model.InterfaceCalls == 0, "default exact dispatch");
            Measure("exact-single", () => bus.PublishAsync(model));
        }

        using (var open = new ServiceCollection().AddMessaging(DiscoveredTypes.All)
                                                 .AddTransient(typeof(IEventHandler<>), typeof(OpenHandler<>)).BuildServiceProvider())
        {
            open.UseMessaging(o => o.Events.EnableInheritance = true);
            var model = new DerivedEvent();
            await model.PublishAsync();
            Check(
                model.OpenCalls == 2 && model.BaseCalls == 1 && model.DerivedCalls == 0 && model.InterfaceCalls == 0,
                "constrained open-generic fan-out and discovery replacement");
        }

        using (var open = new ServiceCollection().AddMessaging(DiscoveredTypes.All)
                                                 .AddTransient(typeof(IEventHandler<>), typeof(MetadataHandler<>)).BuildServiceProvider())
        {
            open.UseMessaging(o => o.Events.EnableInheritance = true);
            var model = new MetadataDerivedEvent();
            string[] expected = ["IEvent", "IUnusedMarker", "MetadataBaseEvent", "MetadataDerivedEvent", "Object"];
            await model.PublishAsync();
            Check(model.Calls.Order().SequenceEqual(expected), "open-generic runtime interface metadata");
            model.Calls.Clear();
            await model.PublishAsync<MetadataBaseEvent>();
            Check(model.Calls.Order().SequenceEqual(expected), "open-generic derived metadata through base contract");
            model.Calls.Clear();
            await model.PublishAsync<IEvent>();
            Check(model.Calls.Order().SequenceEqual(expected), "open-generic derived metadata through IEvent contract");
        }
        await CheckFiltering();
        Console.WriteLine($"Messaging inheritance smoke passed; dynamic code: {RuntimeFeature.IsDynamicCodeSupported}");
    }

    static async Task CheckFiltering()
    {
        foreach (var inheritance in new[] { false, true })
        {
            foreach (var subscriptions in new[] { 4, 256 })
            {
                IEventHandler<DerivedEvent>[] implementations = [new DualHandler(), new OpenHandler<DerivedEvent>()];
                var handlers = Enumerable.Range(0, subscriptions).Select(i => implementations[i % implementations.Length]).ToArray();
                using var provider = new ServiceCollection().AddMessaging(new List<Type>())
                                                            .AddSingleton<IEnumerable<IEventHandler<DerivedEvent>>>(handlers)
                                                            .BuildServiceProvider();
                provider.UseMessaging(o => o.Events.EnableInheritance = inheritance);
                var bus = inheritance ? provider.GetRequiredService<EventBus<DerivedEvent>>() : new(handlers);
                handlers[1] = implementations[0]; // The bus must retain its construction-time snapshot.
                var model = new DerivedEvent();
                var decisions = 0;
                await bus.PublishFilteredAsync(model, _ => ++decisions != 1);
                Check(decisions == 2 && model.DerivedCalls == 0 && model.OpenCalls == subscriptions / 2, "stateful filtering with a handler snapshot");

                var mode = inheritance ? "inherited" : "exact";
                var unfiltered = Measure($"{mode}-many-{subscriptions}", () => bus.PublishAsync(model));
                var filtered = Measure($"{mode}-filtered-many-{subscriptions}", () => bus.PublishFilteredAsync(model, static _ => true));
                Check(filtered == unfiltered, "identity filtering allocated beyond task aggregation");
            }
        }
    }

    static long Measure(string name, Func<Task> publish)
    {
        for (var i = 0; i < 100; i++)
            publish().GetAwaiter().GetResult();
        var stopwatch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            publish().GetAwaiter().GetResult();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        // Many handlers retain the existing Task[]/WhenAll allocation. Single handlers must stay allocation-free.
        if (name.EndsWith("single", StringComparison.Ordinal))
            Check(bytes == 0, name + " allocated on the warmed dispatch path");
        Console.WriteLine($"{name}: {bytes / 10_000d} bytes/publish, {stopwatch.Elapsed.TotalMilliseconds:F2} ms/10000");

        return bytes;
    }

    static void Check(bool success, string message)
    {
        if (!success)
            throw new InvalidOperationException(message);
    }
}

public interface IContract : IEvent
{
    int InterfaceCalls { get; set; }
    int OpenCalls { get; set; }
}

public abstract class BaseEvent : IEvent
{
    public int BaseCalls;
}

public sealed class DerivedEvent : BaseEvent, IContract
{
    public int DerivedCalls;
    public int InterfaceCalls { get; set; }
    public int OpenCalls { get; set; }
}

public sealed class InterfaceOnlyEvent : IContract
{
    public int InterfaceCalls { get; set; }
    public int OpenCalls { get; set; }
}

public sealed class EmptyEvent;

public class ThrowEvent(InvalidOperationException failure) : IEvent
{
    public InvalidOperationException Failure { get; } = failure;
}

public sealed class DerivedThrowEvent(InvalidOperationException failure) : ThrowEvent(failure);

public sealed class ThrowingHandler : IEventHandler<ThrowEvent>
{
    public Task HandleAsync(ThrowEvent model, CancellationToken ct)
        => throw model.Failure;
}

public sealed class DualHandler : IEventHandler<BaseEvent>, IEventHandler<DerivedEvent>
{
    Task IEventHandler<BaseEvent>.HandleAsync(BaseEvent model, CancellationToken ct)
    {
        model.BaseCalls++;

        return Task.CompletedTask;
    }

    public Task HandleAsync(DerivedEvent model, CancellationToken ct)
    {
        model.DerivedCalls++;

        return Task.CompletedTask;
    }
}

public sealed class InterfaceHandler : IEventHandler<IContract>
{
    public Task HandleAsync(IContract model, CancellationToken ct)
    {
        model.InterfaceCalls++;

        return Task.CompletedTask;
    }
}

public sealed class OpenHandler<T> : IEventHandler<T> where T : IContract
{
    public Task HandleAsync(T model, CancellationToken ct)
    {
        model.OpenCalls++;

        return Task.CompletedTask;
    }
}

// Do not reference this interface through a closed handler or cast: it must survive trimming on its own.
public interface IUnusedMarker;

public class MetadataBaseEvent : IEvent
{
    public List<string> Calls { get; } = [];
}

public sealed class MetadataDerivedEvent : MetadataBaseEvent, IUnusedMarker;

public sealed class MetadataHandler<T> : IEventHandler<T> where T : notnull
{
    public Task HandleAsync(T model, CancellationToken ct)
    {
        ((MetadataBaseEvent)(object)model).Calls.Add(typeof(T).Name);

        return Task.CompletedTask;
    }
}