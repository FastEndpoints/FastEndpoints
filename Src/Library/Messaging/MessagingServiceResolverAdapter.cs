namespace FastEndpoints;

/// <summary>
/// adapter that wraps IServiceResolver to implement IMessagingServiceResolver for the messaging package
/// </summary>
sealed class MessagingServiceResolverAdapter : IMessagingServiceResolver
{
    readonly IServiceResolver _resolver;

    public MessagingServiceResolverAdapter(IServiceResolver resolver)
    {
        _resolver = resolver;
    }

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
        => _resolver.CreateInstance(type, serviceProvider);

    public object CreateSingleton(Type type)
        => _resolver.CreateSingleton(type);

    public TService Resolve<TService>() where TService : class
        => _resolver.Resolve<TService>();

    public object Resolve(Type typeOfService)
        => _resolver.Resolve(typeOfService);

    public TService? TryResolve<TService>() where TService : class
        => _resolver.TryResolve<TService>();

    public object? TryResolve(Type typeOfService)
        => _resolver.TryResolve(typeOfService);
}
