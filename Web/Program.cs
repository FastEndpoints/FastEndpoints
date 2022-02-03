global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
using FastEndpoints.Swagger;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services
    .AddSwaggerDoc(s =>
    {
        s.DocumentName = "Initial Release";
        s.Title = "Web API";
        s.Version = "v0.0";
    })
    .AddSwaggerDoc(maxEndpointVersion: 1, settings: s =>
     {
         s.DocumentName = "Release 1.0";
         s.Title = "Web API";
         s.Version = "v1.0";
     })
    .AddSwaggerDoc(maxEndpointVersion: 2, settings: s =>
    {
        s.DocumentName = "Release 2.0";
        s.Title = "FastEndpoints Sandbox";
        s.Version = "v2.0";
    });

var app = builder.Build();
app.UseDefaultExceptionHandler();
app.UseResponseCaching();

app.UseRouting(); //if using, this call must go before auth/cors/fastendpoints middleware

app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
{
    config.SerializerOptions = o => o.PropertyNamingPolicy = null;
    config.EndpointRegistrationFilter = ep => ep.Settings.Tags?.Contains("exclude") is not true;
    config.RoutingOptions = o => o.Prefix = "api";
    config.VersioningOptions = o =>
    {
        o.Prefix = "v";
        //o.DefaultVersion = 1;
        //o.SuffixedVersion = false;
    };
}); //must go before useendpoints if that call is present.

app.UseEndpoints(c => c.MapGet("test", () => "hello world!")); //must go after usefastendpoints (only if using endpoints)

if (!app.Environment.IsProduction())
{
    app.UseOpenApi();
    app.UseSwaggerUi3(s => s.ConfigureDefaults());
}
app.Run();