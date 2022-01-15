global using FastEndpoints;
global using FastEndpoints.Security;
global using FastEndpoints.Validation;
global using Web.Auth;
//using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Http.Json;
using NJsonSchema;
using NJsonSchema.Generation;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Reflection;
using System.Text.RegularExpressions;
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

    });
    app.UseSwaggerUi3(s =>
    {
        s.TagsSorter = "alpha";
    });
}
app.Run();

internal class OperationProcessor : IOperationProcessor
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> descriptions = new()
    {
        { "200", "Success" },
        { "201", "Created" },
        { "202", "Accepted" },
        { "400", "Bad Request" },
        { "401", "Unauthorized" },
        { "403", "Forbidden" },
        { "404", "Not Found" },
        { "405", "Mehtod Not Allowed" },
        { "406", "Not Acceptable" },
        { "500", "Server Error" },
    };

    public bool Process(OperationProcessorContext ctx)
    {
        //use first part of route as tag by default
        var tags = ctx.OperationDescription.Operation.Tags;
        if (tags.Count == 0)
            tags.Add(ctx.OperationDescription.Path.Split('/')[1]);

        var content = ctx.OperationDescription.Operation.Responses.FirstOrDefault().Value.Content;
        if (content?.Count > 0)
        {
            //fix response content-type not displaying correctly. probably a nswag bug. might be fixed in future.
            var contentVal = content.FirstOrDefault().Value;
            content.Clear();
            content.Add(ctx.OperationDescription.Operation.Produces.FirstOrDefault(), contentVal);

            //set response descriptions
            ctx.OperationDescription.Operation.Responses
                .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
                .ToList()
                .ForEach(res =>
                {
                    if (descriptions.ContainsKey(res.Key))
                        res.Value.Description = descriptions[res.Key];
                });
        }

        var op = ctx.OperationDescription.Operation;
        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = apiDescription.HttpMethod == "GET";

        //fix missing path parameters
        ctx.OperationDescription.Path = "/" + apiDescription.RelativePath;

        if (isGETRequest && op.RequestBody is not null)
            op.RequestBody.IsRequired = false;

        //add a param for each url path segment such as /{xxx}/{yyy}
        var reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                Kind = OpenApiParameterKind.Path,
                IsRequired = true,
                Schema = JsonSchema.FromType(typeof(string))
            });

        if (isGETRequest && !reqParams.Any() && reqDtoType is not null)
        {
            //it's a GET request with a request dto and no path params
            //so let's add each dto property as a query param to enable swagger ui to execute GET request with user supplied values

            reqParams = reqDtoProps?
                .Where(p => !p.IsDefined(typeof(FromClaimAttribute), false)) //ignore props marks with [FromClaim]
                .Select(p =>
                    new OpenApiParameter
                    {
                        Name = p.Name,
                        IsRequired = false,
                        Schema = JsonSchema.FromType(p.PropertyType),
                        Kind = OpenApiParameterKind.Query
                    });
        }

        if (reqParams is not null)
        {
            foreach (var p in reqParams)
                op.Parameters.Add(p);
        }

        if (reqDtoProps is not null)
        {
            //add header params if there are any props marked with [FromHeader] attribute
            foreach (var prop in reqDtoProps)
            {
                var attrib = prop.GetCustomAttribute<FromHeaderAttribute>(true);
                if (attrib is not null)
                {
                    op.Parameters.Add(new OpenApiParameter
                    {
                        Name = attrib?.HeaderName ?? prop.Name,
                        IsRequired = attrib?.IsRequired ?? false,
                        Schema = JsonSchema.FromType(prop.PropertyType),
                        Kind = OpenApiParameterKind.Header
                    });
                }
            }
        }

        //var brk = ctx.OperationDescription.Path == "/customer/new";

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