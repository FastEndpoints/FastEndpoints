#if NET7_0_OR_GREATER
    using Microsoft.Net.Http.Headers;

#if NET9_0_OR_GREATER
    using Lock = System.Threading.Lock;
#else
    using Lock = object;
#endif

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
        /// by default, the contents of the request body (form data/json) is taken into consideration when determining the uniqueness of incoming requests even if the
        /// idempotency-key is the same among them. i.e. if two different requests come in with the same idempotency-key but with different request body content, they
        /// will be  considered to be unique requests and the endpoint will be executed for each request.
        /// </summary>
        /// <remarks>
        /// this involves buffering the request body content per each request in order to generate a sha512 hash of the incoming body content. if the clients making
        /// requests are under strict quality control and are guaranteed to not reuse idempotency keys, you can set this to <c>true</c> to prevent the hashing of
        /// request body content.
        /// </remarks>
        public bool IgnoreRequestBody { get; set; }

        /// <summary>
        /// determines how long the cached responses will remain in the cache store before being evicted.
        /// defaults to 10 minutes.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// by default, the idempotency header will be automatically added to the response headers collection. set <c>false</c> to prevent that from happening.
        /// </summary>
        public bool AddHeaderToResponse { get; set; } = true;

        /// <summary>
        /// the description text for the swagger request header parameter
        /// </summary>
        public string? SwaggerHeaderDescription { get; set; }

        /// <summary>
        /// a function to generate an example value for the swagger request param header
        /// </summary>
        public Func<object>? SwaggerExampleGenerator { get; set; }

        /// <summary>
        /// the type/format of the swagger example value
        /// </summary>
        public Type? SwaggerHeaderType { get; set; }

        readonly Lock _lock = new();

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