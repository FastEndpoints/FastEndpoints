var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

public partial class Program;

sealed class MyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c) { }
}