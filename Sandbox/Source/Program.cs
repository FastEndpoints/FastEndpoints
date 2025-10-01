using Void = FastEndpoints.Void;

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("test/{id}");
        AllowAnonymous();
    }

    public override async Task<Void> HandleAsync(CancellationToken c)
    {
        if (Route<int>("id") == 0)
            return await Send.NotFoundAsync();

        return await Send.OkAsync("hello world!");
    }
}