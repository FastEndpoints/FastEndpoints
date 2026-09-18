using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// global configuration for financial-mode HTTP idempotency
/// </summary>
public sealed class FinancialIdempotencyConfig
{
    /// <summary>Trusted stable caller scope inherited by endpoints. Anonymous scopes must be deliberate.</summary>
    public Func<HttpContext, string?>? CallerScope { get; set; }

    /// <summary>Maximum number of retained in-memory reservations. Admission fails at capacity, without evicting unresolved entries.</summary>
    public int InMemoryMaxEntries { get; set; } = 10000;

    /// <summary>
    /// the in-memory store byte budget for completed response bodies and headers. default is 1024 mb.
    /// when this limit is exceeded, <see cref="MemoryFinancialIdempotencyStore" /> refuses <c>Complete</c> so the middleware can fail closed.
    /// this setting is not applicable to custom <see cref="IFinancialIdempotencyStore" /> implementations.
    /// </summary>
    public long InMemoryStoreSize { get; set; } = 1024 * 1024 * 1024;

    /// <summary>
    /// default completed-response retention used when an endpoint does not override <see cref="FinancialIdempotencyOptions.Duration" />.
    /// default is 24 hours.
    /// </summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromHours(24);
}
