using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle events published by the notification system
/// <para>WARNING: event handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TEvent">the type of the event to handle</typeparam>
public abstract class FastEventHandler<TEvent> : IEventHandler<TEvent>, IServiceResolverBase where TEvent : notnull
{
    /// <summary>
    /// this method will be called when an event of the specified type is published.
    /// </summary>
    /// <param name="eventModel">the event model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task HandleAsync(TEvent eventModel, CancellationToken ct);

    /// <summary>
    /// publish the given model/dto to all the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns>a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone"/> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny"/> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll"/> return a Task that will complete only when all of the subscribers complete their work.</returns>
    public Task PublishAsync<TEventModel>(TEventModel eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEventModel : class
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

    #region equality check
    //equality will be checked when discovered concrete handlers are being added to EventBase.handlerDict HashSet<T>
    //we need this check to be done on the type of the handler instead of the default instance equality check
    //to prevent duplicate handlers being added to the hash set if/when multiple instances of the app are being run
    //under the same app domain such as OrchardCore multi-tenancy. ex: https://github.com/FastEndpoints/Library/issues/208
    public override bool Equals(object? obj) => obj?.GetType() == GetType();
    public override int GetHashCode() => GetType().GetHashCode();
    #endregion
}