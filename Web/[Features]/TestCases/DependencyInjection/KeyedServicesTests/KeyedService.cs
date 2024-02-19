namespace TestCases.KeyedServicesTests;

interface IKeyedService
{
    string KeyName { get; init; }
}

sealed class MyKeyedService(string keyName) : IKeyedService
{
    public string KeyName { get; init; } = keyName;
}

sealed class Endpoint : EndpointWithoutRequest<string>
{
    [KeyedService("AAA")]
    public IKeyedService KeyedService { get; set; }

    public override void Configure()
    {
        Get("/test-cases/keyed-services-test");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken c)
        => SendAsync(KeyedService.KeyName);
}