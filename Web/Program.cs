using ASPie;

var builder = WebApplication.CreateBuilder();

var signingKey = builder.Configuration.GetValue<string>("TokenKey");

builder.Services.AddJWTTokenAuthentication(signingKey);
builder.Services.AddAuthorization();
builder.Build()
       .UseASPieWithAuth(builder.Services)
       .Run();
