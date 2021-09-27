using FluentValidation.AspNetCore;
using MapControllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddControllers()
    .AddFluentValidation(fv =>
        fv.RegisterValidatorsFromAssemblyContaining<Controllers>());

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();

namespace MapControllers
{
    public partial class Program { }
}