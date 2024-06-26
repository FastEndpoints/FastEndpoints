using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace FastEndpoints.Testing;

/// <summary>
/// httpclient creation options
/// </summary>
public sealed class ClientOptions
{
    /// <summary>
    /// gets or sets the base address of <see cref="HttpClient" /> instances.
    /// the default is <c>http://localhost</c>.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("http://localhost");

    /// <summary>
    /// setting this value would cause the outgoing request to contain a header with the specified name and a unique value per request, which would allow test code to bypass the
    /// throttling limits enforced by the endpoints. make sure the header name matches with what is configured at the global or endpoint level. if it's not customized, use the
    /// default name <c>X-Forwarded-For</c>
    /// </summary>
    public string? ThrottleBypassHeaderName { get; set; }

    /// <summary>
    /// gets or sets whether <see cref="HttpClient" /> instances should automatically follow redirect responses.
    /// the default is <c>true</c>.
    /// </summary>
    public bool AllowAutoRedirect { get; set; } = true;

    /// <summary>
    /// gets or sets the maximum number of redirect responses that <see cref="HttpClient" /> instances should follow.
    /// the default is <c>7</c>.
    /// </summary>
    public int MaxAutomaticRedirections { get; set; } = 7;

    /// <summary>
    /// gets or sets whether <see cref="HttpClient" /> instances should handle cookies.
    /// the default is <c>true</c>.
    /// </summary>
    public bool HandleCookies { get; set; } = true;

    readonly List<DelegatingHandler> _handlers = [];

    /// <summary>
    /// add delegating handlers to the http client
    /// </summary>
    /// <param name="handlers"></param>
    public void AddHandlers(params DelegatingHandler[] handlers)
        => _handlers.AddRange(handlers);

    internal DelegatingHandler[] CreateHandlers()
    {
        if (AllowAutoRedirect)
            _handlers.Add(new RedirectHandler(MaxAutomaticRedirections));

        if (HandleCookies)
            _handlers.Add(new CookieContainerHandler());

        if (ThrottleBypassHeaderName is not null)
            _handlers.Add(new ThrottleBypassHandler(ThrottleBypassHeaderName));

        return _handlers.ToArray();
    }
}

sealed class ThrottleBypassHandler(string headerName) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(headerName, Guid.NewGuid().ToString("N"));

        return base.SendAsync(request, cancellationToken);
    }
}