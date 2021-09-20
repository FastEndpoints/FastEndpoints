using EZEndpoints;

var builder = WebApplication.CreateBuilder();

builder.Services.AddEZEndpoints();

builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);

builder.Services.AddAuthorization(o => o.AddPolicy("test", b => b.RequireRole("admin")));

var app = builder.Build();
app.UseAuthorization();
app.UseEZEndpoints();
app.Run();