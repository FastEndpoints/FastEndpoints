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
builder.Services.AddFastEndpoints();//(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All);
builder.Services.AddJWTBearerAuth(builder.Configuration["TokenKey"]!);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.DocumentName = "Initial Release";
            s.Title = "Web API";
            s.Version = "v0.0";
            s.SchemaType = NJsonSchema.SchemaType.OpenApi3;
        };
        o.SerializerSettings = x => x.PropertyNamingPolicy = null;
        o.TagCase = TagCase.TitleCase;
        o.RemoveEmptyRequestSchema = false;
    })

    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
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
        };
        o.MaxEndpointVersion = 1;
        o.RemoveEmptyRequestSchema = false;
    })

    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.DocumentName = "Release 2.0";
            s.Title = "FastEndpoints Sandbox";
            s.Version = "v2.0";
        };
        o.MaxEndpointVersion = 2;
        o.RemoveEmptyRequestSchema = false;
    })

    //only ver3 & only FastEndpoints
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.DocumentName = "Release 3.0";
            s.Title = "FastEndpoints Sandbox ver3 only";
            s.Version = "v3.0";
        };
        o.MinEndpointVersion = 3;
        o.MaxEndpointVersion = 3;
        o.ExcludeNonFastEndpoints = true;
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

    c.Binding.ValueParserFor<Guid>(x => new(Guid.TryParse(x?.ToString(), out var res), res));

    c.Endpoints.RoutePrefix = "api";
    c.Endpoints.ShortNames = false;
    c.Endpoints.Filter = ep => ep.EndpointTags?.Contains("exclude") is not true;
    c.Endpoints.Configurator = (ep) =>
    {
        ep.PreProcessors(Order.Before, new AdminHeaderChecker());
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
