using Carter;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Services.AddCarter();
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseRouting();
app.UseAuthorization();
app.UseEndpoints(builder => builder.MapCarter());
app.Run();

namespace CarterModules
{
    public partial class Program { }
}