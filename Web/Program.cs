using ASPie;

var x = WebApplication.CreateBuilder();

x.Build()
 .UseASPie(x.Services)
 .Run();
