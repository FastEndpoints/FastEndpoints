namespace TestCases.X402;

sealed class Response
{
    public string Message { get; set; } = string.Empty;
}

sealed class VerifyDto
{
    [JsonPropertyName("paymentPayload")]
    public JsonObject? PaymentPayload { get; set; }

    [JsonPropertyName("paymentRequirements")]
    public JsonObject? PaymentRequirements { get; set; }
}
