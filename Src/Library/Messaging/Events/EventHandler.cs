using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle events published by the notification system
/// <para>WARNING: event handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TEvent">the type of the event to handle</typeparam>
[HideFromDocs]
public abstract class FastEventHandler<TEvent> : IEventHandler<TEvent>, IEventBus, IServiceResolverBase where TEvent : notnull
{
    //todo: this class can be deprecated in the future as no longer mentioned in the docs
    //      currently only here to not break existing consumers

    /// <summary>
    /// this method will be called when an event of the specified type is published.
    /// </summary>
    /// <param name="eventModel">the event model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task HandleAsync(TEvent eventModel, CancellationToken ct);

    ///<inheritdoc/>
    public Task PublishAsync<TEventModel>(TEventModel eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEventModel : notnull
        => Config.ServiceResolver.Resolve<Event<TEventModel>>().PublishAsync(eventModel, waitMode, cancellation);

    ///<inheritdoc/>
    public TService? TryResolve<TService>() where TService : class => Config.ServiceResolver.TryResolve<TService>();
    ///<inheritdoc/>
    public object? TryResolve(Type typeOfService) => Config.ServiceResolver.TryResolve(typeOfService);
    ///<inheritdoc/>
    public TService Resolve<TService>() where TService : class => Config.ServiceResolver.Resolve<TService>();
    ///<inheritdoc/>
    public object Resolve(Type typeOfService) => Config.ServiceResolver.Resolve(typeOfService);
    ///<inheritdoc/>
    public IServiceScope CreateScope() => Config.ServiceResolver.CreateScope();
}