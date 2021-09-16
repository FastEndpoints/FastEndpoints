using ASPie;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseASPie();
app.Run();
