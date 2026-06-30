using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FastEndpoints;

public static class KestrelExtensions
{
    /// <summary>
    /// enable inter-process-communication via unix domain sockets instead of tcp transport when everything is running on the same machine.
    /// a unix socket will be created with the provided <paramref name="serviceName" />
    /// </summary>
    /// <param name="ko"></param>
    /// <param name="serviceName">
    /// a unique name to identity this service. clients must use the same name in order to connect to this server with the
    /// <c>.MapRemote()</c> call.
    /// </param>
    /// <param name="o">kestrel listen options</param>
    public static void ListenInterProcess(this KestrelServerOptions ko, string serviceName, Action<ListenOptions>? o = null)
    {
        ko.ListenUnixSocket(Path.Combine(Path.GetTempPath(), $"{serviceName.ToLowerInvariant()}.sock"), DefaultListenOpts + o);

        static void DefaultListenOpts(ListenOptions lo)
            => lo.Protocols = HttpProtocols.Http2;
    }
}