namespace TestCases.CommandBusTest;

sealed class ReceiverRequest
{
    public string Name { get; set; }
}

sealed class ReceiverEndpoint : Endpoint<ReceiverRequest>
{
    public override void Configure()
    {
        Get("/tests/command/receivers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ReceiverRequest r, CancellationToken c)
    {
        await new VoidCommand { FirstName = r.Name, LastName = r.Name }.ExecuteAsync(ct: c);
        await Send.OkAsync();
    }
}