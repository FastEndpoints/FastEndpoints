using FluentValidation;
using MvcControllers;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddControllers();
builder.Services.AddSingleton<IValidator<Request>, Validator>();

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();

namespace MvcControllers
{
    public partial class Program { }
}