using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// financial-mode idempotency settings for an endpoint.
/// this is a reservation protocol, not output-cache fingerprinting. do not call <c>Idempotency()</c> on the same endpoint.
/// </summary>
public sealed class FinancialIdempotencyOptions
{
    /// <summary>
    /// the header name that will contain the idempotency key. defaults to <c>Idempotency-Key</c>.
    /// missing or duplicate values result in <c>400</c>.
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    TimeSpan? _duration;

    /// <summary>
    /// Retention of completed responses. Unresolved reservations never expire automatically.
    /// </summary>
    public TimeSpan Duration
    {
        get => _duration ?? TimeSpan.FromHours(24);
        set => _duration = value;
    }

    /// <summary>
    /// Trusted stable tenant/account scope, evaluated after authentication. Required globally or per endpoint.
    /// </summary>
    public Func<HttpContext, string?>? CallerScope { get; set; }

    /// <summary>
    /// Maximum request key length. Default is 256 characters.
    /// </summary>
    public int MaxKeyLength { get; set; } = 256;

    /// <summary>
    /// Server-owned settlement timeout, independent of request cancellation.
    /// </summary>
    public TimeSpan SettlementTimeout { get; set; } = TimeSpan.FromSeconds(10);

    List<string>? _identityHeaders;

    internal List<string> IdentityHeaders => _identityHeaders ?? FinancialIdempotencyHeaders.IdentityHeaderNames(HeaderName, AdditionalHeaders);

    internal static bool IsValidDuration(TimeSpan duration)
        => duration > TimeSpan.Zero && duration <= TimeSpan.FromDays(36500);

    internal void ApplyDefaults(FinancialIdempotencyConfig config)
    {
        _duration ??= config.DefaultDuration;
        CallerScope ??= config.CallerScope;

        if (CallerScope is null ||
            !IsValidDuration(Duration) ||
            MaxResponseBodySize <= 0 ||
            MaxResponseBodySize > int.MaxValue ||
            MaxKeyLength <= 0 ||
            SettlementTimeout <= TimeSpan.Zero ||
            SettlementTimeout.TotalMilliseconds > uint.MaxValue - 1 ||
            ReplayStatusCode is not null and (< 200 or > 299))
            throw new InvalidOperationException("Invalid financial idempotency options or missing CallerScope!");
        if (AdditionalHeaders is null)
            throw new InvalidOperationException("AdditionalHeaders cannot be null!");

        foreach (var name in AdditionalHeaders.Append(HeaderName))
        {
            if (string.IsNullOrEmpty(name) || name.Any(c => !char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-.^_`|~".Contains(c)))
                throw new InvalidOperationException("Invalid financial idempotency header name!");
        }
        _identityHeaders = FinancialIdempotencyHeaders.IdentityHeaderNames(HeaderName, AdditionalHeaders);
    }

    /// <summary>
    /// when set, replayed 2xx responses are written with this status instead of the original.
    /// the original status is still stored. not applied to the first execution.
    /// </summary>
    public int? ReplayStatusCode { get; set; }

    /// <summary>
    /// by default, the idempotency header is echoed on the response. set <c>false</c> to prevent that.
    /// </summary>
    public bool AddHeaderToResponse { get; set; } = true;

    /// <summary>
    /// additional request headers that participate in the identity key.
    /// No additional headers are included by default.
    /// transport noise (<c>User-Agent</c>, <c>Accept</c>, <c>Content-Type</c>, <c>Host</c>, ...) is never identity, even if added here.
    /// </summary>
    public HashSet<string> AdditionalHeaders { get; set; } = [];

    /// <summary>
    /// set to <c>true</c> if request paths are case-sensitive for identity. default is case-insensitive.
    /// independent of fingerprint <see cref="IdempotencyConfig.UseCaseSensitivePaths" />.
    /// </summary>
    public bool UseCaseSensitivePaths { get; set; }

    /// <summary>
    /// the largest capturable size of a 2xx response body. default is 128 mb.
    /// Overflow interrupts execution and retains the reservation (fail closed).
    /// </summary>
    public long MaxResponseBodySize { get; set; } = 128 * 1024 * 1024;

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
}