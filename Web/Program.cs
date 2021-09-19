using ASPie;

var x = WebApplication.CreateBuilder();

x.Build()
 .UseASPieWithAuth(x.Services)
 .Run();
