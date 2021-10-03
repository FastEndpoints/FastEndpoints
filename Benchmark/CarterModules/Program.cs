using Carter;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCarter();
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthorization();
app.UseRouting();
app.UseEndpoints(builder => builder.MapCarter());
app.Run();

namespace CarterModules
{
    public partial class Program { }
}