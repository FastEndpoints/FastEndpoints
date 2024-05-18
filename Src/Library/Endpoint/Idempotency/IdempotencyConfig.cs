#if NET7_0_OR_GREATER
    namespace FastEndpoints;

    public sealed class IdempotencyConfig
    {
        /// <summary>
        /// the in-memory output cache storage size. default value is 1024 mb.
        /// when this limit is exceeded, no new responses will be cached until older entries are evicted.
        /// this setting will not be applicable if using some other cache store such as redis.
        /// </summary>
        public long InMemoryCacheSize { get; set; } = 1024 * 1024 * 1024;

        /// <summary>
        /// the largest cacheable size of the response body. default is set to 128 mb.
        /// if the response body exceeds this limit, it will not be cached.
        /// </summary>
        public long MaxResponseBodySize { get; set; } = 128 * 1024 * 1024;

        /// <summary>
        /// set to <c>true</c> if request paths are case-sensitive. default is to treat paths as case-insensitive.
        /// </summary>
        public bool UseCaseSensitivePaths { get; set; }
    }
#endif