using FluentValidation.AspNetCore;
using MvcControllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddMvc()
    .AddFluentValidation(fv =>
        fv.RegisterValidatorsFromAssemblyContaining<Validator>());

var app = builder.Build();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

namespace MvcControllers
{
    public partial class Program { }
}