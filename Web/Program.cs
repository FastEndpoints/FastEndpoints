global using FastEndpoints;
global using FastEndpoints.Security;
global using Web.Auth;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Localization;
using NSwag;
using System.Globalization;
using Web.PipelineBehaviors.PreProcessors;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]!);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services
    .AddSwaggerDoc(s =>
    {
        s.DocumentName = "Initial Release";
        s.Title = "Web API";
        s.Version = "v0.0";
    },
    serializerSettings: x => x.PropertyNamingPolicy = null)

    .AddSwaggerDoc(maxEndpointVersion: 1, settings: s =>
     {
         s.DocumentName = "Release 1.0";
         s.Title = "Web API";
         s.Version = "v1.0";
         s.AddAuth("ApiKey", new()
         {
             Name = "api_key",
             In = OpenApiSecurityApiKeyLocation.Header,
             Type = OpenApiSecuritySchemeType.ApiKey,
         });
     })

    .AddSwaggerDoc(maxEndpointVersion: 2, settings: s =>
    {
        s.DocumentName = "Release 2.0";
        s.Title = "FastEndpoints Sandbox";
        s.Version = "v2.0";
    })

    //only ver3 & only FastEndpoints
    .AddSwaggerDoc(minEndpointVersion: 3, maxEndpointVersion: 3, excludeNonFastEndpoints: true, settings: s =>
    {
        s.DocumentName = "Release 3.0";
        s.Title = "FastEndpoints Sandbox ver3 only";
        s.Version = "v3.0";
    });

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseDefaultExceptionHandler();
app.UseResponseCaching();

app.UseRouting(); //if using, this call must go before auth/cors/fastendpoints middleware

app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Serializer.Options.PropertyNamingPolicy = null;

    c.Endpoints.RoutePrefix = "api";
    c.Endpoints.ShortNames = false;
    c.Endpoints.Filter = ep => ep.EndpointTags?.Contains("exclude") is not true;
    c.Endpoints.Configurator = (ep) =>
    {
        ep.PreProcessors(new AdminHeaderChecker());
        if (ep.EndpointTags?.Contains("orders") is true)
            ep.Description(b => b.Produces<ErrorResponse>(400, "application/problem+json"));
    };

    c.Versioning.Prefix = "ver";

    c.Throttle.HeaderName = "X-Custom-Throttle-Header";
    c.Throttle.Message = "Custom Error Response";
});

//this must go after usefastendpoints (only if using endpoints)
app.UseEndpoints(c =>
{
    c.MapGet("test", () => "hello world!").WithTags("map-get");
    c.MapGet("test/{testId:int?}", (int? testId) => $"hello {testId}").WithTags("map-get");
});

if (!app.Environment.IsProduction())
{
    app.UseOpenApi();
    app.UseSwaggerUi3(s => s.ConfigureDefaults());
}
app.Run();

public partial class Program { }
