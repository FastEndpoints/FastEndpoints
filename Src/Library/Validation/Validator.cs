using FluentValidation;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit from this base class to define your dto validators
/// <para>
/// HINT: validators are registered as singletons. i.e. the same validator instance is used to validate each request for best performance. hance,
/// do not maintain state in your validators.
/// </para>
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class Validator<TRequest> : AbstractValidator<TRequest>, IServiceResolverBase, IEndpointValidator where TRequest : notnull
{
    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => ServiceResolver.Instance.CreateScope();

    /// <inheritdoc />
    public TService? TryResolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    /// <inheritdoc />
    public TService Resolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    /// <inheritdoc />
    public object Resolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);
}