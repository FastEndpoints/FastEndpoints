global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
//using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using NJsonSchema.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
//using Swashbuckle.AspNetCore.SwaggerUI;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddCors();
builder.Services.AddResponseCaching();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddScoped<IEmailService, EmailService>();
//builder.Services.AddSwagger();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(s =>
{
    s.Title = "this is the title";
    s.Version = new("2.1.1");
    s.SchemaNameGenerator = new SchemaNameGenerator();
    s.OperationProcessors.Add(new OperationProcessor());
});
builder.Services.AddMvcCore().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

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
    app.UseOpenApi(s =>
    {
        s.Path = "/nswag/{documentName}/swagger.json";
    });
    app.UseSwaggerUi3(s =>
    {
        s.Path = "/nswag";
        s.SwaggerRoutes.Clear();
        s.SwaggerRoutes.Add(new("v1", "/nswag/v1/swagger.json"));
    });
}
app.Run();

internal class OperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext ctx)
    {
        var tags = ctx.OperationDescription.Operation.Tags;
        if (!tags.Any())
            tags.Add(ctx.OperationDescription.Path.Split('/')[1]);

        return true;
    }
}

internal class SchemaNameGenerator : ISchemaNameGenerator
{
    public string? Generate(Type type)
    {
        return type.FullName?.Replace(".", "_");
    }
}