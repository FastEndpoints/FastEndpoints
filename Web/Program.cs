using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Http.Json;
using Web.Auth;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddCors();
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddScoped<IEmailService, EmailService>();

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors();
app.UseFastEndpoints();
app.Run();

//todo: write tests
// - AcceptFiles()
// - SendBytesAsync()