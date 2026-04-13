using System.Net.Http.Json;

namespace FastEndpoints;

/// <summary>
/// abstraction for communicating with a x402 facilitator service.
/// </summary>
public interface IX402FacilitatorClient
{
    /// <summary>
    /// verifies the supplied payment payload against the supplied payment requirements.
    /// </summary>
    /// <param name="request">the verification request payload</param>
    /// <param name="ct">cancellation token</param>
    /// <returns>the verification result returned by the facilitator</returns>
    Task<VerificationResponse> VerifyAsync(VerificationRequest request, CancellationToken ct);

    /// <summary>
    /// settles the supplied payment payload against the supplied payment requirements.
    /// </summary>
    /// <param name="request">the settlement request payload</param>
    /// <param name="ct">cancellation token</param>
    /// <returns>the settlement result returned by the facilitator</returns>
    Task<SettlementResponse> SettleAsync(SettlementRequest request, CancellationToken ct);
}

sealed class X402FacilitatorClient(HttpClient client) : IX402FacilitatorClient
{
    public async Task<VerificationResponse> VerifyAsync(VerificationRequest request, CancellationToken ct)
        => await PostAsync<VerificationRequest, VerificationResponse>("verify", request, ct);

    public async Task<SettlementResponse> SettleAsync(SettlementRequest request, CancellationToken ct)
        => await PostAsync<SettlementRequest, SettlementResponse>("settle", request, ct);

    async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken ct)
    {
        using var res = await client.PostAsJsonAsync(path, request, X402Serializer.Options, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (res.IsSuccessStatusCode)
        {
            return System.Text.Json.JsonSerializer.Deserialize<TResponse>(body, X402Serializer.Options) ??
                   throw new InvalidOperationException($"facilitator returned an empty [{typeof(TResponse).Name}] response!");
        }

        if (typeof(TResponse) == typeof(SettlementResponse))
        {
            SettlementResponse? settlement = null;

            try
            {
                settlement = System.Text.Json.JsonSerializer.Deserialize<SettlementResponse>(body, X402Serializer.Options);
            }
            catch (System.Text.Json.JsonException) { }

            if (settlement is not null)
                return (TResponse)(object)settlement;
        }

        if (typeof(TResponse) == typeof(VerificationResponse))
        {
            VerificationResponse? verification = null;

            try
            {
                verification = System.Text.Json.JsonSerializer.Deserialize<VerificationResponse>(body, X402Serializer.Options);
            }
            catch (System.Text.Json.JsonException) { }

            if (verification is not null)
                return (TResponse)(object)verification;
        }

        throw new InvalidOperationException($"facilitator call [{path}] failed with status [{(int)res.StatusCode}]: {body}");
    }
}
