using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test JsonIgnore and other serialization attributes - likely AOT issue
public sealed class JsonIgnoreRequest
{
    public string Username { get; set; } = "";
    
    [JsonIgnore]
    public string Password { get; set; } = "";
    
    [JsonPropertyName("email_address")]
    public string Email { get; set; } = "";
}

public sealed class JsonIgnoreResponse
{
    public string Username { get; set; } = "";
    
    [JsonIgnore]
    public string InternalId { get; set; } = "";
    
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";
    
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public int Score { get; set; }
}

public sealed class JsonIgnoreEndpoint : Endpoint<JsonIgnoreRequest, JsonIgnoreResponse>
{
    public override void Configure()
    {
        Post("json-ignore-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonIgnoreRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonIgnoreResponse
        {
            Username = req.Username,
            InternalId = Guid.NewGuid().ToString(), // Should not appear in response
            DisplayName = req.Username.ToUpperInvariant(),
            Score = 100
        });
    }
}
