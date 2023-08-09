using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Messaging.Remote;

internal static class ObjectFactoryExtensions
{
    internal static T GetFromProviderOrCreateInstance<T>(this ObjectFactory factory, IServiceProvider provider)
        => provider.GetService<T>() ?? (T)factory(provider, null);
}