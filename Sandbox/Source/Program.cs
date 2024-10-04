var bld = WebApplication.CreateBuilder(args);
bld.Services
   .AddFastEndpoints()
   .SwaggerDocument();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await SendAsync("ok...");
    }
}

//public partial class Program { }