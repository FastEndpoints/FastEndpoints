using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Messaging.Remote;

static class ObjectFactoryExtensions
{
    internal static THandler GetEventHandlerOrCreateInstance<TEvent, THandler>(this ObjectFactory factory, IServiceProvider provider)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
            => provider.GetService<IEventHandler<TEvent>>() as THandler ?? (THandler)factory(provider, null);
}