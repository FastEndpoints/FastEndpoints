#pragma warning disable CA1822
using FastEndpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// This class will contains the basic functionality and helpers methods to be used in the EventHandler and CommandHandler classes 
/// <para>WARNING: handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
public abstract class HandlerBase
{
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
        => IServiceResolver.RootServiceProvider.GetRequiredService<Event<TEventModel>>().PublishAsync(eventModel, waitMode, cancellation);

    /// <summary>
    /// send the given model/dto to the registered handler of the command
    /// </summary>
    /// <param name="commandModel">the command model/dto to handle</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns/>a Task of the response result that matches the command type.
    public Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> commandModel, CancellationToken cancellation = default)
        => commandModel.ExecuteAsync(cancellation);

    /// <summary>
    /// send the given model/dto to the registered handler of the command
    /// </summary>
    /// <param name="commandModel">the command model/dto to handle</param>
    ///<param name="cancellation">an optional cancellation token</param>
    public Task ExecuteAsync(ICommand commandModel, CancellationToken cancellation = default)
        => commandModel.ExecuteAsync(cancellation);

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolve<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService) => IServiceResolver.RootServiceProvider.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService) => IServiceResolver.RootServiceProvider.GetRequiredService(typeOfService);
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

    #region equality check
    //equality will be checked when discovered concrete handlers are being added to EventBase.handlerDict HashSet<T>
    //we need this check to be done on the type of the handler instead of the default instance equality check
    //to prevent duplicate handlers being added to the hash set if/when multiple instances of the app are being run
    //under the same app domain such as OrchardCore multi-tenancy. ex: https://github.com/FastEndpoints/Library/issues/208
    public override bool Equals(object? obj) => obj?.GetType() == GetType();
    public override int GetHashCode() => GetType().GetHashCode();
    #endregion
}