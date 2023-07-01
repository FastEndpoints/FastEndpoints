global using FastEndpoints;
global using FastEndpoints.Security;
global using Web.Auth;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Localization;
using NSwag;
using System.Globalization;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.ServerStreamingTest;
using TestCases.UnitTestConcurrencyTest;
using Web.PipelineBehaviors.PreProcessors;
using Web.Services;

var bld = WebApplication.CreateBuilder();
bld.AddHandlerServer();
bld.Services
   .AddCors()
   .AddResponseCaching()
   .AddFastEndpoints()//(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All);
   .AddJWTBearerAuth(bld.Configuration["TokenKey"]!)
   .AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)))
   .AddScoped<IEmailService, EmailService>()
   .AddSingleton(new SingltonSVC(0))
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
   .SwaggerDocument(o => //only ver3 & only FastEndpoints
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

var supportedCultures = new[] { new CultureInfo("en-US") };

var app = bld.Build();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
})
   .UseDefaultExceptionHandler()
   .UseResponseCaching()
   .UseRouting() //if using, this call must go before auth/cors/fastendpoints middleware
   .UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())
   .UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(c =>
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
   })
   .UseEndpoints(c => //this must go after usefastendpoints (only if using endpoints)
   {
       c.MapGet("test", () => "hello world!").WithTags("map-get");
       c.MapGet("test/{testId:int?}", (int? testId) => $"hello {testId}").WithTags("map-get");
   });

if (!app.Environment.IsProduction())
{
    app.UseSwaggerGen();
}

app.MapHandlers(h =>
{
    h.Register<TestVoidCommand, TestVoidCommandHandler>();
    h.Register<TestCommand, TestCommandHandler, string>();
    h.Register<EchoCommand, EchoCommandHandler, EchoCommand>();
    h.RegisterServerStream<StatusStreamCommand, StatusUpdateHandler, StatusUpdate>();
    h.RegisterClientStream<CurrentPosition, PositionProgressHandler, ProgressReport>();
});

app.Run();

public partial class Program { }
