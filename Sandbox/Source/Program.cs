var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

sealed class SimpleGater : IFeatureFlag
{
    public string? Name { get; set; }

    public Task<bool> IsEnabledAsync(IEndpoint endpoint)
        => Task.FromResult(Name == "HelloWorld");
}

sealed class BetaEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/hello");
        AllowAnonymous();
        FeatureFlag<SimpleGater>("HelloWorld");
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await Send.OkAsync("hello world!");
    }
}