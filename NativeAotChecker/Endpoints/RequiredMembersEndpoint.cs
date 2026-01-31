using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with required keyword (C# 11+)
public class RequiredMembersRequest
{
    public required string RequiredName { get; set; }
    public required int RequiredId { get; set; }
    public string? OptionalDescription { get; set; }
    public required List<string> RequiredTags { get; set; }
}

// Response
public class RequiredMembersResponse
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool RequiredMembersBound { get; set; }
}

/// <summary>
/// Tests 'required' keyword members in AOT mode.
/// AOT ISSUE: 'required' keyword enforcement uses constructor parameter analysis.
/// Required member validation at deserialization uses reflection.
/// RequiredMemberAttribute handling needs runtime metadata.
/// </summary>
public class RequiredMembersEndpoint : Endpoint<RequiredMembersRequest, RequiredMembersResponse>
{
    public override void Configure()
    {
        Post("required-members-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequiredMembersRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new RequiredMembersResponse
        {
            Name = req.RequiredName,
            Id = req.RequiredId,
            Description = req.OptionalDescription,
            Tags = req.RequiredTags,
            RequiredMembersBound = !string.IsNullOrEmpty(req.RequiredName) && req.RequiredTags.Count > 0
        });
    }
}
