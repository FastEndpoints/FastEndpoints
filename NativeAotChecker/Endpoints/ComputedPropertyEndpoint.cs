using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with computed/calculated properties
public class ComputedPropertyRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int BirthYear { get; set; }
    public List<int> Scores { get; set; } = [];
}

// Response with various computed properties
public class ComputedPropertyResponse
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    // Computed property - not settable
    public string FullName => $"{FirstName} {LastName}";
    
    // Computed with JsonIgnore on setter
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NullableComputed { get; set; }
    
    public int BirthYear { get; set; }
    
    // Computed age
    public int Age => DateTime.UtcNow.Year - BirthYear;
    
    public List<int> Scores { get; set; } = [];
    
    // Computed statistics
    public double AverageScore => Scores.Count > 0 ? Scores.Average() : 0;
    public int MaxScore => Scores.Count > 0 ? Scores.Max() : 0;
    public int TotalScore => Scores.Sum();
    
    public bool ComputedPropertiesWorked { get; set; }
}

/// <summary>
/// Tests computed/calculated properties in AOT mode.
/// AOT ISSUE: Get-only properties with computed values may not serialize correctly.
/// Expression-bodied members like 'FullName => ...' need property getter preservation.
/// JsonIgnoreCondition evaluation uses reflection for property inspection.
/// </summary>
public class ComputedPropertyEndpoint : Endpoint<ComputedPropertyRequest, ComputedPropertyResponse>
{
    public override void Configure()
    {
        Post("computed-property-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ComputedPropertyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ComputedPropertyResponse
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            BirthYear = req.BirthYear,
            Scores = req.Scores,
            NullableComputed = req.Scores.Count > 0 ? "Has scores" : null,
            ComputedPropertiesWorked = true
        });
    }
}
