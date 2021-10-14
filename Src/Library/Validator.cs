using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Validation;

/// <summary>
/// inherit from this base class to define your dto validators
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Validator<TRequest> : AbstractValidator<TRequest>, IHasServiceProvider where TRequest : class
{
#pragma warning disable CS8618
    public IServiceProvider ServiceProvider { get; set; } //set from .UseFastEndpoints() upon initialization
#pragma warning restore CS8618

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    protected TService? TryResolve<TService>() => ServiceProvider.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    protected object? TryResolve(Type typeOfService) => ServiceProvider.GetService(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    protected TService Resolve<TService>() where TService : notnull => ServiceProvider.GetRequiredService<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    protected object Resolve(Type typeOfService) => ServiceProvider.GetRequiredService(typeOfService);
}

