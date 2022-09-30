using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal interface IServiceResolver
{
    static IServiceProvider RootServiceProvider { get; set; } //set only from .UseFastEndpoints() during startup

    IServiceScope CreateScope();

    TService? TryResolve<TService>() where TService : class;
    object? TryResolve(Type typeOfService);

    TService Resolve<TService>() where TService : class;
    object Resolve(Type typeOfService);
}
