namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class Request
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

sealed class Response
{
    public string FullName { get; set; }
}

sealed class EndpointWithArrayResponse : Endpoint<Request[], IEnumerable<Response>>
{
    public override void Configure()
    {
        Post("ser-ctx-gen-for-collection-dtos");
        Description(x => x.Accepts<Request[]>("application/json"));
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request[] req, CancellationToken ct)
    {
        var response = req.Select(r => new Response { FullName = $"{r.FirstName} {r.LastName}" }).ToList();
        await Send.OkAsync(response);
    }
}