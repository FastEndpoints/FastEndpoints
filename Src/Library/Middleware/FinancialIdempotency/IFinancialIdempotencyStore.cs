namespace FastEndpoints;

/// <summary>
/// outcome of an atomic <see cref="IFinancialIdempotencyStore.TryBeginAsync" /> call
/// </summary>
public enum FinancialBeginKind
{
    /// <summary>
    /// the key was free and is now reserved as in-flight. the handler may run.
    /// </summary>
    Started,

    /// <summary>
    /// a completed 2xx for the same payload is stored. replay it and do not run the handler.
    /// </summary>
    Replay,

    /// <summary>
    /// the key was already used with a different payload hash.
    /// </summary>
    Conflict,

    /// <summary>
    /// an uncertain outcome cannot be replayed. fail closed.
    /// </summary>
    Unreplayable,

    /// <summary>
    /// another request is currently executing for this key.
    /// all stores must return this immediately; middleware maps it to <c>409</c> in-progress.
    /// </summary>
    InFlight
}

/// <summary>
/// result of <see cref="IFinancialIdempotencyStore.TryBeginAsync" />
/// </summary>
public readonly struct FinancialBeginResult
{
    /// <summary>
    /// the state-machine outcome
    /// </summary>
    public FinancialBeginKind Kind { get; }

    /// <summary>
    /// the stored 2xx when <see cref="Kind" /> is <see cref="FinancialBeginKind.Replay" />; otherwise <see langword="null" />
    /// </summary>
    public FinancialIdempotencyResponse? Response { get; }

    /// <summary>Opaque ownership token, present only for Started.</summary>
    public string? ReservationToken { get; }

    FinancialBeginResult(FinancialBeginKind kind, FinancialIdempotencyResponse? response, string? token = null)
    {
        ReservationToken = token;
        Kind = kind;
        Response = response;
    }

    /// <summary>
    /// the key was reserved as in-flight
    /// </summary>
    public static FinancialBeginResult Started(string token)
        => new(FinancialBeginKind.Started, null, token);

    /// <summary>
    /// replay the stored 2xx
    /// </summary>
    public static FinancialBeginResult Replay(FinancialIdempotencyResponse response)
        => new(FinancialBeginKind.Replay, response);

    /// <summary>
    /// same key, different payload
    /// </summary>
    public static FinancialBeginResult Conflict()
        => new(FinancialBeginKind.Conflict, null);

    /// <summary>
    /// original outcome cannot be replayed
    /// </summary>
    public static FinancialBeginResult Unreplayable()
        => new(FinancialBeginKind.Unreplayable, null);

    /// <summary>
    /// a request with this key is already executing
    /// </summary>
    public static FinancialBeginResult InFlight()
        => new(FinancialBeginKind.InFlight, null);
}

/// <summary>
/// a captured 2xx HTTP response stored for financial idempotency replay
/// </summary>
public sealed class FinancialIdempotencyResponse
{
    /// <summary>
    /// the original http status code
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// response headers to replay. hop-by-hop headers should already have been omitted.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string[]>> Headers { get; init; } = [];

    /// <summary>
    /// the captured response body
    /// </summary>
    public byte[] Body { get; init; } = [];
}

/// <summary>
/// storage contract for financial-mode HTTP idempotency.
/// <para>
/// <see cref="TryBeginAsync" /> MUST be atomic against other <see cref="TryBeginAsync" />,
/// <see cref="CompleteAsync" />, <see cref="AbandonAsync" />, and <see cref="MarkUnreplayableAsync" />
/// calls for the same identity key. implementors who skip atomicity will double-execute handlers
/// (including payment charges) under concurrency. this is the same class of uniqueness requirement as
/// <c>IJobStorageProvider.StoreJobAsync</c>.
/// </para>
/// <para>
/// typical implementations: Redis <c>SET NX</c> / Lua, SQL unique insert, etc.
/// FastEndpoints does not ship a Redis provider. the in-memory store is single-process only.
/// </para>
/// </summary>
public interface IFinancialIdempotencyStore
{
    /// <summary>
    /// atomically look up or reserve <paramref name="identityKey" />.
    /// <list type="table">
    ///     <listheader>
    ///         <term>existing</term><description>same hash / different hash</description>
    ///     </listheader>
    ///     <item>
    ///         <term>missing</term><description>insert in-flight, return <see cref="FinancialBeginKind.Started" /></description>
    ///     </item>
    ///     <item>
    ///         <term>in-flight</term><description><see cref="FinancialBeginKind.InFlight" /> / <see cref="FinancialBeginKind.Conflict" /></description>
    ///     </item>
    ///     <item>
    ///         <term>completed</term><description><see cref="FinancialBeginKind.Replay" /> / <see cref="FinancialBeginKind.Conflict" /></description>
    ///     </item>
    ///     <item>
    ///         <term>unreplayable</term><description><see cref="FinancialBeginKind.Unreplayable" /> / <see cref="FinancialBeginKind.Conflict" /></description>
    ///     </item>
    /// </list>
    /// ttl applies only to completed responses. Active and uncertain reservations never expire automatically.
    /// </summary>
    ValueTask<FinancialBeginResult> TryBeginAsync(string identityKey,
                                                  ReadOnlyMemory<byte> payloadHash,
                                                  TimeSpan ttl,
                                                  CancellationToken ct);

    /// <summary>
    /// Atomically complete only the matching active owner. Retention begins now. Never downgrade a settled record.
    /// </summary>
    Task<FinancialSettlementResult> CompleteAsync(string identityKey, string reservationToken, FinancialIdempotencyResponse response, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// Atomically retain an uncertain outcome indefinitely for the matching active owner.
    /// </summary>
    Task<FinancialSettlementResult> MarkUnreplayableAsync(string identityKey, string reservationToken, CancellationToken ct);

    /// <summary>
    /// Release only the matching active owner after an explicit no-business-side-effects assertion.
    /// </summary>
    Task<FinancialSettlementResult> AbandonAsync(string identityKey, string reservationToken, CancellationToken ct);
}

/// <summary>Result of an atomic ownership-checked settlement.</summary>
public enum FinancialSettlementResult
{
    /// <summary>
    /// The transition was applied.
    /// </summary>
    Applied,

    /// <summary>
    /// The matching owner already settled. The record is unchanged.
    /// </summary>
    AlreadySettled,

    /// <summary>
    /// The reservation is missing or owned by another token.
    /// </summary>
    OwnershipLost
}