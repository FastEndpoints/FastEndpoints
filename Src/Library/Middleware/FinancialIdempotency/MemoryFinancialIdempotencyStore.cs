using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// Bounded single-process storage. Restart loses protection; production distributed use requires durable application storage.
/// </summary>
public sealed class MemoryFinancialIdempotencyStore : IFinancialIdempotencyStore
{
    readonly Lock _gate = new();
    readonly Dictionary<string, Record> _records = new(StringComparer.Ordinal);
    readonly TimeProvider _time;
    readonly ILogger<MemoryFinancialIdempotencyStore>? _logger;
    readonly long _budget;
    readonly int _maxEntries;
    long _used;

    /// <summary>
    /// Create a store with a total body/header budget and bounded reservation count. Unresolved entries are never evicted.
    /// </summary>
    public MemoryFinancialIdempotencyStore(long maxStoredBodyBytes = 1024L * 1024 * 1024,
                                           ILogger<MemoryFinancialIdempotencyStore>? logger = null,
                                           TimeProvider? time = null,
                                           int maxEntries = 10000)
    {
        if (maxStoredBodyBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxStoredBodyBytes));
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        _budget = maxStoredBodyBytes;
        _maxEntries = maxEntries;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<FinancialBeginResult> TryBeginAsync(string identityKey, ReadOnlyMemory<byte> payloadHash, TimeSpan ttl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (identityKey.Length is 0 or > 1024 || payloadHash.Length is 0 or > 128)
            throw new ArgumentException("Reservation metadata exceeds supported limits.");

        lock (_gate)
        {
            if (_records.TryGetValue(identityKey, out var record))
            {
                if (record.State != RecordState.Completed || record.Expires > _time.GetUtcNow())
                {
                    if (!CryptographicOperations.FixedTimeEquals(record.Hash, payloadHash.Span))
                        return new(FinancialBeginResult.Conflict());

                    return new(record.State switch
                    {
                        RecordState.Active => FinancialBeginResult.InFlight(),
                        RecordState.Completed => FinancialBeginResult.Replay(Clone(record.Response!)),
                        RecordState.Unreplayable => FinancialBeginResult.Unreplayable(),
                        _ => throw new InvalidOperationException($"Unknown financial reservation state: {record.State}")
                    });
                }

                _used -= record.Size;
                _records.Remove(identityKey);
            }

            if (_records.Count >= _maxEntries)
            {
                Reclaim();

                if (_records.Count >= _maxEntries)
                {
                    _logger?.LogError("Financial reservation capacity exhausted. Reconciliation is required.");

                    throw new InvalidOperationException("Financial reservation capacity exhausted. Reconciliation is required.");
                }
            }

            var token = Guid.NewGuid().ToString("N");
            _records.Add(identityKey, new() { Hash = payloadHash.ToArray(), Token = token });

            return new(FinancialBeginResult.Started(token));
        }
    }

    /// <inheritdoc />
    public Task<FinancialSettlementResult> CompleteAsync(string identityKey, string reservationToken, FinancialIdempotencyResponse response, TimeSpan ttl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!FinancialIdempotencyOptions.IsValidDuration(ttl))
            throw new ArgumentOutOfRangeException(nameof(ttl));

        lock (_gate)
        {
            Reclaim();
            var result = Check(identityKey, reservationToken, out var record);

            if (result != FinancialSettlementResult.Applied)
                return Task.FromResult(result);

            var size = response.Body.LongLength;

            checked
            {
                foreach (var header in response.Headers)
                {
                    size += header.Key.Length * 2L + 32;
                    foreach (var value in header.Value)
                        size += value.Length * 2L + 24;
                }
            }

            if (size > _budget - _used)
            {
                _logger?.LogError("Financial response storage capacity exhausted.");

                throw new InvalidOperationException("Financial response storage capacity exhausted.");
            }

            record!.Response = Clone(response);
            record.Size = size;
            record.Expires = _time.GetUtcNow() + ttl;
            record.State = RecordState.Completed;
            _used += size;

            return Task.FromResult(FinancialSettlementResult.Applied);
        }
    }

    /// <inheritdoc />
    public Task<FinancialSettlementResult> MarkUnreplayableAsync(string identityKey, string reservationToken, CancellationToken ct)
        => Settle(identityKey, reservationToken, false, ct);

    /// <inheritdoc />
    public Task<FinancialSettlementResult> AbandonAsync(string identityKey, string reservationToken, CancellationToken ct)
        => Settle(identityKey, reservationToken, true, ct);

    Task<FinancialSettlementResult> Settle(string key, string token, bool abandon, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var result = Check(key, token, out var record);

            if (result == FinancialSettlementResult.Applied)
            {
                if (abandon)
                    _records.Remove(key);
                else
                    record!.State = RecordState.Unreplayable;
            }

            return Task.FromResult(result);
        }
    }

    FinancialSettlementResult Check(string key, string token, out Record? record)
    {
        if (!_records.TryGetValue(key, out record) || record.Token != token)
            return FinancialSettlementResult.OwnershipLost;

        return record.State == RecordState.Active ? FinancialSettlementResult.Applied : FinancialSettlementResult.AlreadySettled;
    }

    void Reclaim()
    {
        var now = _time.GetUtcNow();

        foreach (var pair in _records.Where(p => p.Value.State == RecordState.Completed && p.Value.Expires <= now).ToArray())
        {
            _used -= pair.Value.Size;
            _records.Remove(pair.Key);
        }
    }

    static FinancialIdempotencyResponse Clone(FinancialIdempotencyResponse response)
        => new()
        {
            StatusCode = response.StatusCode, Body = response.Body.ToArray(),
            Headers = response.Headers.Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray())).ToArray()
        };

    enum RecordState
    {
        Active,
        Completed,
        Unreplayable
    }

    sealed class Record
    {
        public required byte[] Hash;
        public required string Token;
        public RecordState State = RecordState.Active;
        public DateTimeOffset Expires;
        public long Size;
        public FinancialIdempotencyResponse? Response;
    }
}