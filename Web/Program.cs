global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
using FastEndpoints.Swashbuckle;
//using FastEndpoints.NSwag;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddSwashbuckle(tagIndex: 1, options: o =>
{
    o.SwaggerDoc(
        documentName: "v1",
        info: new()
        {
            Title = "FastEndpoints Sandbox",
            Version = "1.0"
        },
        apiGroupNames: new[] { "v1", VersioningOptions.Common });
    o.SwaggerDoc(
        documentName: "v2",
        info: new()
        {
            Title = "FastEndpoints Sandbox",
            Version = "2.0"
        },
        apiGroupNames: new[] { "v2", VersioningOptions.Common });
});

//builder.Services.AddNSwag(s =>
//{
//    s.DocumentName = "v1";
//    s.Title = "FastEndpoints Sandbox";
//    s.Version = "v1.0";
//    s.ApiGroupNames = new[] { "v1", VersioningOptions.Common };
//});
//builder.Services.AddNSwag(s =>
//{
//    s.DocumentName = "v2";
//    s.Title = "FastEndpoints Sandbox";
//    s.Version = "v2.0";
//    s.ApiGroupNames = new[] { "v2", VersioningOptions.Common };
//});

var app = builder.Build();
app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();
app.UseFastEndpoints(config =>
{
    config.SerializerOptions = o => o.PropertyNamingPolicy = null;
    config.EndpointRegistrationFilter = ep => ep.Tags?.Contains("exclude") is not true;
    config.RoutingOptions = o => o.Prefix = "api";
    config.VersioningOptions = o =>
    {
        o.DefaultVersion = VersioningOptions.Common;
        o.Prefix = "v";
    };
});

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.ConfigureDefaults();
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        o.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
    });

    //app.UseOpenApi();
    //app.UseSwaggerUi3(s => s.ConfigureDefaults());
}
app.Run();