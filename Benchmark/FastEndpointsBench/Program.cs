using FastEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run("http://localhost:5000");

namespace FastEndpointsBench
{
    public partial class Program { }
}