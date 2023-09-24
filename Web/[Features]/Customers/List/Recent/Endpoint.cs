namespace Customers.List.Recent;

public class Endpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/customer/list/recent");
        Policies("AdminOnly");
        Roles(
            Role.Admin,
            Role.Staff);
        Permissions(
            Allow.Customers_Retrieve,
            Allow.Customers_Create);
        AccessControl("Customers_Retrieve", "Admin");
        Options(o => o.Produces<Response>(200));
        Version(0, deprecateAt: 1);
    }

    public override Task<object?> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult((object?)new Response
        {
            Customers = new[] {
                    new KeyValuePair<string,int>("ryan gunner", 123),
                    new KeyValuePair<string,int>("debby ryan", 124),
                    new KeyValuePair<string,int>("ryan reynolds",321)
                }
        });
    }
}

public class Response
{
    public IEnumerable<KeyValuePair<string, int>>? Customers { get; set; }
}

public class Endpoint_V1 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Version(1, deprecateAt: 2);
        AuthSchemes("ApiKey", "Cookies");
    }
}

public class Endpoint_V2 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Version(2, deprecateAt: 3);
    }
}

public class Endpoint_V3 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Version(3, deprecateAt: 4);
    }
}