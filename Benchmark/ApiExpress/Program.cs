using ApiExpress;

var builder = WebApplication.CreateBuilder();
builder.Services.AddApiExpress();

var app = builder.Build();
app.UseAuthorization();
app.UseApiExpress();
app.Run();

namespace ApiExpressBench
{
    public partial class Program { }
}