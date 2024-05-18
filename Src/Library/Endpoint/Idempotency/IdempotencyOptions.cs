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
        /// </summary>
        public HashSet<string> AdditionalHeaders { get; set; } = [];

        /// <summary>
        /// by default, the contents of the request body does not participate in the cache-key generation in order to provide the best possible performance.
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
    }
#endif