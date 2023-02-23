using FastEndpoints;
using FEBench;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Services.AddFastEndpoints();
builder.Services.AddScoped<ScopedValidator>();

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();

namespace FEBench
{
    public partial class Program { }
}