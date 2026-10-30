namespace FastEndpoints;

/// <summary>
/// stable error codes set on financial idempotency error responses, so clients don't need to parse messages.
/// they are serialized with <see cref="ProblemDetails" /> when <c>IndicateErrorCode</c> is enabled, and are available to custom error response builders.
/// </summary>
public static class FinancialIdempotencyErrorCodes
{
    /// <summary>
    /// <c>409</c> with <c>Retry-After</c>: another request with this key is still executing. retry after the indicated delay.
    /// </summary>
    public const string InProgress = "FinancialIdempotency.InProgress";

    /// <summary>
    /// <c>422</c>: the key was already used with a different payload. retrying the same request will not succeed.
    /// </summary>
    public const string PayloadMismatch = "FinancialIdempotency.PayloadMismatch";

    /// <summary>
    /// <c>409</c> without <c>Retry-After</c>: the outcome of the original request is unknown and cannot be replayed.
    /// reconcile the outcome before retrying, because a retry after retention expiry runs the handler again.
    /// </summary>
    public const string OutcomeUnknown = "FinancialIdempotency.OutcomeUnknown";
}
