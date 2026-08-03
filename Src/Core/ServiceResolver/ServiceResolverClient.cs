using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// Default <see cref="IServiceResolverBase"/> implementation that forwards to <see cref="ServiceResolver.Instance"/>.
/// Inherit this type when the class hierarchy allows; otherwise call the static <see cref="Forward"/> helpers.
/// </summary>
public abstract class ServiceResolverClient : IServiceResolverBase
{
    /// <summary>
    /// Shared forwarders for types that cannot inherit <see cref="ServiceResolverClient"/>
    /// (e.g. validators that already inherit FluentValidation, or structs).
    /// </summary>
    internal static class Forward
    {
        public static TService? TryResolve<TService>() where TService : class
            => ServiceResolver.Instance.TryResolve<TService>();

        public static object? TryResolve(Type typeOfService)
            => ServiceResolver.Instance.TryResolve(typeOfService);

        public static TService Resolve<TService>() where TService : class
            => ServiceResolver.Instance.Resolve<TService>();

        public static object Resolve(Type typeOfService)
            => ServiceResolver.Instance.Resolve(typeOfService);

        public static IServiceScope CreateScope()
            => ServiceResolver.Instance.CreateScope();

        public static TService? TryResolve<TService>(string keyName) where TService : class
            => ServiceResolver.Instance.TryResolve<TService>(keyName);

        public static object? TryResolve(Type typeOfService, string keyName)
            => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

        public static TService Resolve<TService>(string keyName) where TService : class
            => ServiceResolver.Instance.Resolve<TService>(keyName);

        public static object Resolve(Type typeOfService, string keyName)
            => ServiceResolver.Instance.Resolve(typeOfService, keyName);
    }

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => Forward.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => Forward.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => Forward.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => Forward.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => Forward.CreateScope();

    /// <inheritdoc />
    public TService? TryResolve<TService>(string keyName) where TService : class
        => Forward.TryResolve<TService>(keyName);

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService, string keyName)
        => Forward.TryResolve(typeOfService, keyName);

    /// <inheritdoc />
    public TService Resolve<TService>(string keyName) where TService : class
        => Forward.Resolve<TService>(keyName);

    /// <inheritdoc />
    public object Resolve(Type typeOfService, string keyName)
        => Forward.Resolve(typeOfService, keyName);
}
