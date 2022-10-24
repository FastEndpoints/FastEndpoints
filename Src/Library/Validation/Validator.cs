﻿using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit from this base class to define your dto validators
/// <para>HINT: validators are registered as singletons. i.e. the same validator instance is used to validate each request for best performance. hance, do not maintain state in your validators.</para>
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Validator<TRequest> : AbstractValidator<TRequest>, IServiceResolver, IEndpointValidator where TRequest : class
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolve<TService>() where TService : class
        => IServiceResolver.RootServiceProvider.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService)
        => IServiceResolver.RootServiceProvider.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class
        => IServiceResolver.RootServiceProvider.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService)
        => IServiceResolver.RootServiceProvider.GetRequiredService(typeOfService);
    /// <summary>
    /// if you'd like to resolve scoped or transient services from the DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    public IServiceScope CreateScope()
        => IServiceResolver.RootServiceProvider.CreateScope();
}