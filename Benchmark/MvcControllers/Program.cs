using FluentValidation;
using MvcControllers;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddSingleton<IValidator<Request>, Validator>()
    .AddMvc();

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