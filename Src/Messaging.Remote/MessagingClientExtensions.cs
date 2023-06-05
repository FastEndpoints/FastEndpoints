using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

public static class MessagingClientExtensions
{
    //key: tCommand
    //val: list of remote servers that has handlers listening
    internal static readonly Dictionary<Type, List<RemoteServerConfiguration>> CommandsToRemotesMap = new();

    public static IHost MapRemoteHandlers(this IHost host, string serverAddress, Action<RemoteServerConfiguration> r)
    {
        r(new RemoteServerConfiguration(serverAddress, host.Services));
        return host;
    }
}