using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with JsonPropertyName for aliasing
public class JsonPropertyNameRequest
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;
    
    [JsonPropertyName("email_address")]
    public string EmailAddress { get; set; } = string.Empty;
    
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    
    // Property without alias
    public int Age { get; set; }
}

public class JsonPropertyNameResponse
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;
    
    [JsonPropertyName("email_address")]
    public string EmailAddress { get; set; } = string.Empty;
    
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }
    
    public int Age { get; set; }
    public bool JsonPropertyNameWorked { get; set; }
}

/// <summary>
/// Tests [JsonPropertyName] attribute for JSON property aliasing in AOT mode.
/// AOT ISSUE: JsonPropertyName attribute discovery uses reflection.
/// Property-to-JSON mapping requires GetCustomAttribute calls.
/// Source generator must preserve these attribute mappings.
/// </summary>
public class JsonPropertyNameEndpoint : Endpoint<JsonPropertyNameRequest, JsonPropertyNameResponse>
{
    public override void Configure()
    {
        Post("json-property-name-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonPropertyNameRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonPropertyNameResponse
        {
            UserName = req.UserName,
            EmailAddress = req.EmailAddress,
            PhoneNumber = req.PhoneNumber,
            Age = req.Age,
            JsonPropertyNameWorked = !string.IsNullOrEmpty(req.UserName) && !string.IsNullOrEmpty(req.EmailAddress)
        });
    }
}
