global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
//using FastEndpoints.Swashbuckle;
using FastEndpoints.NSwag;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();
//builder.Services.AddSwashbuckle();
builder.Services.AddNSwag(x => x.Title = "FastEndpoints Sandbox");

var app = builder.Build();
app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();
app.UseFastEndpoints(config =>
{
    config.SerializerOptions = o => o.PropertyNamingPolicy = null;
    config.EndpointRegistrationFilter = ep => ep.Tags?.Contains("exclude") is not true;
    //config.ErrorResponseBuilder = failures => $"there are {failures.Count()} validation issues!";
    // config.VersioningOptions = o =>
    // {
    //     o.Prefix = "v";
    //     o.DefaultVersion = "1"; 
    // };
});

if (!app.Environment.IsProduction())
{
    //app.UseSwagger();
    //app.UseSwaggerUI(o => o.ConfigureDefaults());

    app.UseOpenApi();
    app.UseSwaggerUi3(s => s.ConfigureDefaults());
}
app.Run();