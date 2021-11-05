global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using Swashbuckle.AspNetCore.SwaggerUI;
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
    o.SerializerOptions.AddContext<Admin.Login.ReqJsonCtx>();
    o.SerializerOptions.AddContext<Admin.Login.ResJsonCtx>();
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