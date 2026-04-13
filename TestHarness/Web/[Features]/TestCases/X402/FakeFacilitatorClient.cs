namespace TestCases.X402;

sealed class FakeFacilitatorClient : IX402FacilitatorClient
{
    public Task<VerificationResponse> VerifyAsync(VerificationRequest request, CancellationToken ct)
    {
        if (request.X402Version != 2)
            return Task.FromResult(new VerificationResponse { IsValid = false, InvalidReason = "invalid_x402_version" });

        var payToken = request.PaymentPayload.Payload?["testToken"]?.GetValue<string>();

        return Task.FromResult(
            payToken == "valid" || payToken == "settle-first"
                ? new() { IsValid = true, Payer = "0xpayer" }
                : new VerificationResponse { IsValid = false, InvalidReason = "invalid_test_payment" });
    }

    public Task<SettlementResponse> SettleAsync(SettlementRequest request, CancellationToken ct)
    {
        if (request.X402Version != 2)
            return Task.FromResult(new SettlementResponse { Success = false, ErrorReason = "invalid_x402_version" });

        var payToken = request.PaymentPayload.Payload?["testToken"]?.GetValue<string>();

        return Task.FromResult(
            payToken == "valid" || payToken == "settle-first"
                ? new()
                {
                    Success = true,
                    Transaction = "0xtx",
                    Network = request.PaymentRequirements.Network,
                    Payer = "0xpayer"
                }
                : new SettlementResponse { Success = false, ErrorReason = "settlement_failed" });
    }
}
