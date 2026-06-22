using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
        => await PostAsync("verify", request, X402Serializer.Context.VerificationRequest, X402Serializer.Context.VerificationResponse, ct);

    public async Task<SettlementResponse> SettleAsync(SettlementRequest request, CancellationToken ct)
        => await PostAsync("settle", request, X402Serializer.Context.SettlementRequest, X402Serializer.Context.SettlementResponse, ct);

    async Task<TResponse> PostAsync<TRequest, TResponse>(string path,
                                                         TRequest request,
                                                         JsonTypeInfo<TRequest> requestTypeInfo,
                                                         JsonTypeInfo<TResponse> responseTypeInfo,
                                                         CancellationToken ct)
    {
        using var res = await client.PostAsJsonAsync(BuildUri(path), request, requestTypeInfo, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (res.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize(body, responseTypeInfo) ??
                   throw new InvalidOperationException($"facilitator returned an empty [{typeof(TResponse).Name}] response!");
        }

        if (typeof(TResponse) == typeof(SettlementResponse))
        {
            SettlementResponse? settlement = null;

            try
            {
                settlement = JsonSerializer.Deserialize(body, X402Serializer.Context.SettlementResponse);
            }
            catch (JsonException) { }

            if (settlement is not null)
                return (TResponse)(object)settlement;
        }

        if (typeof(TResponse) == typeof(VerificationResponse))
        {
            VerificationResponse? verification = null;

            try
            {
                verification = JsonSerializer.Deserialize(body, X402Serializer.Context.VerificationResponse);
            }
            catch (JsonException) { }

            if (verification is not null)
                return (TResponse)(object)verification;
        }

        throw new InvalidOperationException($"facilitator call [{path}] failed with status [{(int)res.StatusCode}]: {body}");
    }

    string BuildUri(string path)
    {
        if (client.BaseAddress is null)
            return path;

        var baseUrl = client.BaseAddress.AbsoluteUri.TrimEnd('/');

        return $"{baseUrl}/{path}";
    }
}