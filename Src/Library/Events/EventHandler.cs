using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle events published by the notification system
/// </summary>
/// <typeparam name="TEvent">the type of the event to handle</typeparam>
public abstract class FastEventHandler<TEvent> : IEventHandler<TEvent> where TEvent : notnull
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
        => Event<TEventModel>.PublishAsync(eventModel, waitMode, cancellation);

    /// <summary>
    /// if you'd like to resolve scoped or transient services from the DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    public IServiceScope CreateScope() => IServiceResolver.RootServiceProvider.CreateScope();

    /// <summary>
    /// resolve a singleton of the given type from the dependency injection container. will throw if unresolvable.
    /// <para>WARNING: do not resolve scoped/transient dependancies using this method. use <see cref="CreateScope"/> instead.</para> 
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService ResolveSingleton<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetRequiredService<TService>();

    /// <summary>
    /// resolve a singleton of the given type from the dependency injection container. will throw if unresolvable.
    /// <para>WARNING: do not resolve scoped/transient dependancies using this method. use <see cref="CreateScope"/> instead.</para> 
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object ResolveSingleton(Type typeOfService) => IServiceResolver.RootServiceProvider.GetRequiredService(typeOfService);

    /// <summary>
    /// try to resolve a singleton of the given type from the dependency injection container. will return null if unresolvable.
    /// <para>WARNING: do not resolve scoped/transient dependancies using this method. use <see cref="CreateScope"/> instead.</para>
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolveSingleton<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetService<TService>();

    /// <summary>
    /// try to resolve a singleton of the given type from the dependency injection container. will return null if unresolvable.
    /// <para>WARNING: do not resolve scoped/transient dependancies using this method. use <see cref="CreateScope"/> instead.</para>
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolveSingleton(Type typeOfService) => IServiceResolver.RootServiceProvider.GetService(typeOfService);
}