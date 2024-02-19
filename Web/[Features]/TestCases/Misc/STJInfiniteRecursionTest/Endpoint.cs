namespace TestCases.STJInfiniteRecursionTest;

sealed class Response
{
    public int Id { get; set; }
    public Response? Res { get; set; }
}

sealed class Endpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/testcases/stj-infinite-recursion");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var response = new Response();
        var currentResponse = response;

        for (int i = 0; i < 100; i++)
        {
            currentResponse.Id = i;
            currentResponse.Res = new Response();
            currentResponse = currentResponse.Res;
        }

        await SendAsync(response);
    }
}