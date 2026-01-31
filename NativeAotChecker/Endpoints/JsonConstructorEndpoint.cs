using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Class with JsonConstructor attribute
public class JsonConstructorRequest
{
    public string Name { get; }
    public int Value { get; }
    public string? Description { get; set; }
    
    // Default constructor for AOT
    public JsonConstructorRequest() 
    {
        Name = string.Empty;
        Value = 0;
    }
    
    [JsonConstructor]
    public JsonConstructorRequest(string name, int value)
    {
        Name = name;
        Value = value;
    }
}

// Immutable class requiring constructor
public class ImmutableRequest
{
    public string Id { get; }
    public string Title { get; }
    public IReadOnlyList<string> Tags { get; }
    
    public ImmutableRequest()
    {
        Id = string.Empty;
        Title = string.Empty;
        Tags = [];
    }
    
    [JsonConstructor]
    public ImmutableRequest(string id, string title, IReadOnlyList<string> tags)
    {
        Id = id;
        Title = title;
        Tags = tags;
    }
}

public class JsonConstructorResponse
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string? Description { get; set; }
    public string ImmutableId { get; set; } = string.Empty;
    public string ImmutableTitle { get; set; } = string.Empty;
    public int TagCount { get; set; }
    public bool JsonConstructorWorked { get; set; }
}

/// <summary>
/// Tests [JsonConstructor] attribute for parameterized constructors in AOT mode.
/// AOT ISSUE: JsonConstructor discovery uses reflection to find marked constructors.
/// Constructor parameter mapping requires ParameterInfo reflection.
/// Immutable types with constructor-only initialization need runtime analysis.
/// </summary>
public class JsonConstructorEndpoint : Endpoint<JsonConstructorRequest, JsonConstructorResponse>
{
    public override void Configure()
    {
        Post("json-constructor-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonConstructorRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonConstructorResponse
        {
            Name = req.Name,
            Value = req.Value,
            Description = req.Description,
            JsonConstructorWorked = !string.IsNullOrEmpty(req.Name) && req.Value > 0
        });
    }
}

/// <summary>
/// Tests immutable types with JsonConstructor in AOT mode.
/// </summary>
public class ImmutableTypeEndpoint : Endpoint<ImmutableRequest, JsonConstructorResponse>
{
    public override void Configure()
    {
        Post("immutable-type-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ImmutableRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonConstructorResponse
        {
            ImmutableId = req.Id,
            ImmutableTitle = req.Title,
            TagCount = req.Tags.Count,
            JsonConstructorWorked = !string.IsNullOrEmpty(req.Id) && req.Tags.Count > 0
        });
    }
}
