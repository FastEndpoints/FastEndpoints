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
//builder.Services.AddSwashbuckle(tagIndex: 2, options: o =>
//{
//    o.SwaggerDoc("v1", new() { Title = "Api v1", Version = "v1" });
//    o.SwaggerDoc("v2", new() { Title = "Api v2", Version = "v2" });
//});
builder.Services.AddNSwag(s =>
{
    s.Title = "FastEndpoints Sandbox";
    s.Version = "v1.0";
    s.DocumentName = "v1";
    s.ApiGroupNames = new[] { "v1", VersioningOptions.Common };
});
builder.Services.AddNSwag(s =>
{
    s.Title = "FastEndpoints Sandbox";
    s.Version = "v2.0";
    s.DocumentName = "v2";
    s.ApiGroupNames = new[] { "v2", VersioningOptions.Common };
});

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
    //config.RequestDeserializer = async (req, tDto, ct) =>
    //{
    //    using var reader = new StreamReader(req.Body);
    //    return Newtonsoft.Json.JsonConvert.DeserializeObject(await reader.ReadToEndAsync(), tDto);
    //};
    //config.ResponseSerializer = (rsp, dto, cType, ct) =>
    //{
    //    rsp.ContentType = cType;
    //    return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
    //};
    //config.ErrorResponseBuilder = failures => $"there are {failures.Count()} validation issues!";
});

if (!app.Environment.IsProduction())
{
    //app.UseSwagger();
    //app.UseSwaggerUI(o =>
    //{
    //    o.ConfigureDefaults();
    //    o.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    //    o.SwaggerEndpoint("/swagger/v2/swagger.json", "API V2");
    //});

    app.UseOpenApi();
    app.UseSwaggerUi3(s => s.ConfigureDefaults());
}
app.Run();