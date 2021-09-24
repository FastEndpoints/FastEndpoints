using FluentValidation.AspNetCore;
using MvcController;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddControllers()
    .AddFluentValidation(fv =>
        fv.RegisterValidatorsFromAssemblyContaining<Controllers>());

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();

namespace MvcController
{
    public partial class Program { }
}