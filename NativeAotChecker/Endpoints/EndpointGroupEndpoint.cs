using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Endpoint configuration group
public class ApiGroup : Group
{
    public ApiGroup()
    {
        Configure("api/v1", ep =>
        {
            ep.Description(x => x
                .Produces(401)
                .WithTags("api-v1"));
        });
    }
}

// Sub-group inheriting from parent group
public class UsersGroup : SubGroup<ApiGroup>
{
    public UsersGroup()
    {
        Configure("users", ep =>
        {
            ep.Description(x => x
                .Produces(403)
                .WithTags("users"));
        });
    }
}

// Request/Response for grouped endpoint
public class GroupedEndpointRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public class GroupedEndpointResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string FullRoute { get; set; } = string.Empty;
    public List<string> AppliedGroups { get; set; } = [];
    public bool GroupWorked { get; set; }
}

/// <summary>
/// Tests endpoint configuration groups in AOT mode.
/// AOT ISSUE: Group&lt;TGroup&gt; uses generic type lookup.
/// SubGroup inheritance uses reflection for parent discovery.
/// Group configuration merging uses reflection.
/// </summary>
public class EndpointGroupEndpoint : Endpoint<GroupedEndpointRequest, GroupedEndpointResponse>
{
    public override void Configure()
    {
        Post("profile");
        AllowAnonymous();
        Group<UsersGroup>();
    }

    public override async Task HandleAsync(GroupedEndpointRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new GroupedEndpointResponse
        {
            UserId = req.UserId,
            Action = req.Action,
            FullRoute = "/api/v1/users/profile",
            AppliedGroups = ["ApiGroup", "UsersGroup"],
            GroupWorked = true
        });
    }
}
