using FluentValidation;
using MapControllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton<IValidator<Request>, Validator>()
    .AddControllers();

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();

namespace MapControllers
{
    public partial class Program { }
}