﻿using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle events published by the notification system
/// <para>WARNING: event handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TEvent">the type of the event to handle</typeparam>
[HideFromDocs, Obsolete("Implement IEventHandler<T> interface instead.")]
public abstract class FastEventHandler<TEvent> : IEventHandler<TEvent>, IEventBus, IServiceResolverBase where TEvent : notnull
{
    //todo: remove this class in next major version jump

    /// <summary>
    /// this method will be called when an event of the specified type is published.
    /// </summary>
    /// <param name="eventModel">the event model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task HandleAsync(TEvent eventModel, CancellationToken ct);

    /// <inheritdoc />
    public Task PublishAsync<TEventModel>(TEventModel eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
        where TEventModel : notnull
        => Cfg.ServiceResolver.Resolve<EventBus<TEventModel>>().PublishAsync(eventModel, waitMode, cancellation);

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => Cfg.ServiceResolver.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => Cfg.ServiceResolver.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => Cfg.ServiceResolver.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => Cfg.ServiceResolver.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => Cfg.ServiceResolver.CreateScope();

    /// <inheritdoc />
    public TService? TryResolve<TService>(string keyName) where TService : class
        => Cfg.ServiceResolver.TryResolve<TService>(keyName);

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService, string keyName)
        => Cfg.ServiceResolver.TryResolve(typeOfService, keyName);

    /// <inheritdoc />
    public TService Resolve<TService>(string keyName) where TService : class
        => Cfg.ServiceResolver.Resolve<TService>(keyName);

    /// <inheritdoc />
    public object Resolve(Type typeOfService, string keyName)
        => Cfg.ServiceResolver.Resolve(typeOfService, keyName);
}