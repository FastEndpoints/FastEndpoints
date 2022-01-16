global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
using FastEndpoints.NSwag;
using Microsoft.AspNetCore.Http.Json;
using Web.Services;
//using FastEndpoints.Swashbuckle;
//using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddScoped<IEmailService, EmailService>();
//builder.Services.AddSwashbuckle();
builder.Services.AddNSwag(x => x.Title = "FastEndpoints Sandbox");

var app = builder.Build();
app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();
app.UseFastEndpoints();

if (!app.Environment.IsProduction())
{
    //app.UseSwagger();
    //app.UseSwaggerUI(o =>
    //{
    //    o.DocExpansion(DocExpansion.None);
    //    o.DefaultModelExpandDepth(0);
    //});

    app.UseOpenApi();
    app.UseSwaggerUi3(s =>
    {
        s.CustomInlineStyles = ".servers-title,.servers{display:none} .swagger-ui .info{margin:10px 0} .swagger-ui .scheme-container{margin:10px 0;padding:10px 0}";
        s.AdditionalSettings["filter"] = true;
        s.AdditionalSettings["persistAuthorization"] = true;
        s.AdditionalSettings["displayRequestDuration"] = true;
        s.AdditionalSettings["tryItOutEnabled"] = true;
        s.TagsSorter = "alpha";
    });
}
app.Run();