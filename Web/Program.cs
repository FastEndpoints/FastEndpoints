global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null;
    o.SerializerOptions.AddContext<SerializerContext>();
});
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSwagger();

var app = builder.Build();
app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();
app.UseFastEndpoints();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.DocExpansion(DocExpansion.None);
        o.DefaultModelExpandDepth(0);
    });
}
app.Run();

[
    JsonSerializable(typeof(Admin.Login.Request)),
    JsonSerializable(typeof(Admin.Login.Response)),
    JsonSerializable(typeof(Customers.Create.Request)),
    JsonSerializable(typeof(Customers.List.Recent.Response))
]
public partial class SerializerContext : JsonSerializerContext { }