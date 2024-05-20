using Microsoft.Net.Http.Headers;

#if NET7_0_OR_GREATER
namespace FastEndpoints;

/// <summary>
/// idempotency settings for an endpoint
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// the header name that will contain the idempotency key. defaults to <c>Idempotency-Key</c>
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// any additional headers that should participate in the generation of the cache-key.
    /// see the source/definition for the list of default additional headers.
    /// </summary>
    public HashSet<string> AdditionalHeaders { get; set; } =
    [
        HeaderNames.Accept,
        HeaderNames.AcceptEncoding,
        HeaderNames.Authorization,
        HeaderNames.CacheControl,
        HeaderNames.Connection,
        HeaderNames.ContentLength,
        HeaderNames.ContentType,
        HeaderNames.Cookie,
        HeaderNames.Host,
        HeaderNames.KeepAlive,
        HeaderNames.UserAgent
    ];

    /// <summary>
    /// by default, the contents of the request body (form data/json) does not participate in the cache-key generation in order to provide the best possible performance.
    /// by setting this to <c>false</c>, you can make the request body content a contributor to the cache-key generation.
    /// i.e. multiple responses will be cached for the same <c>idempotency key</c> if the contents of the request body is different.
    /// </summary>
    public bool IgnoreRequestBody { get; set; } = true;

    /// <summary>
    /// determines how long the cached responses will remain in the cache store before being evicted.
    /// defaults to 10 minutes.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// by default, the idempotency header will be automatically added to the response headers collection. set <c>false</c> to prevent that from happenning.
    /// </summary>
    public bool AddHeaderToResponse { get; set; } = true;

    public string? SwaggerHeaderDescription { get; set; }
    public Func<object>? SwaggerExampleGenerator { get; set; }
    public Type? SwaggerHeaderType { get; set; }

    readonly object _lock = new();
    bool? _isMultipartFormRequest;

    internal bool? IsMultipartFormRequest
    {
        get => _isMultipartFormRequest;
        set
        {
            lock (_lock) //in case multiple requests come in at the same time for this endpoint
            {
                _isMultipartFormRequest = value;

                if (value is false)
                    return;

                AdditionalHeaders.Remove(HeaderNames.ContentType); //because boundary values are different in each request
            }
        }
    }
}
#endif