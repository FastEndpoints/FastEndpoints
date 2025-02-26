using System.Globalization;
using FastEndpoints.Swagger;
using NJsonSchema;
using NSwag;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;
using TestCases.EventHandlingTest;
using TestCases.EventQueueTest;
using TestCases.JobQueueTest;
using TestCases.KeyedServicesTests;
using TestCases.ProcessorStateTest;
using TestCases.ServerStreamingTest;
using TestCases.UnitTestConcurrencyTest;
using Web;
using Web.PipelineBehaviors.PreProcessors;
using Web.Services;

var bld = WebApplication.CreateBuilder(args);
bld.AddHandlerServer();
bld.Services
   .AddCors()
   .AddIdempotency()
   .AddResponseCaching()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddAuthenticationJwtBearer(s => s.SigningKey = bld.Configuration["TokenKey"]!)
   .AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)))
   .AddKeyedTransient<IKeyedService>("AAA", (_, _) => new MyKeyedService("AAA"))
   .AddKeyedTransient<IKeyedService>("BBB", (_, _) => new MyKeyedService("BBB"))
   .AddScoped<IEmailService, EmailService>()
   .AddSingleton(new SingltonSVC(0))
   .AddJobQueues<Job, JobStorage>()
   .RegisterServicesFromWeb()
   .AddAntiforgery()
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Initial Release";
                   s.Title = "Web API";
                   s.Version = "v0.0";
                   s.SchemaSettings.SchemaType = SchemaType.OpenApi3;
               };
           o.TagCase = TagCase.TitleCase;
           o.TagStripSymbols = true;
           o.RemoveEmptyRequestSchema = false;
       })
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 1.0";
                   s.Title = "Web API";
                   s.Version = "v1.0";
                   s.AddAuth(
                       "ApiKey",
                       new()
                       {
                           Name = "api_key",
                           In = OpenApiSecurityApiKeyLocation.Header,
                           Type = OpenApiSecuritySchemeType.ApiKey
                       });
               };
           o.MaxEndpointVersion = 1;
           o.RemoveEmptyRequestSchema = false;
           o.TagStripSymbols = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 2.0";
                   s.Title = "FastEndpoints Sandbox";
                   s.Version = "v2.0";
               };
           o.MaxEndpointVersion = 2;
           o.ShowDeprecatedOps = true;
           o.RemoveEmptyRequestSchema = false;
           o.TagStripSymbols = true;
       })
   .SwaggerDocument(
       o => //only ver3 & only FastEndpoints
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 3.0";
                   s.Title = "FastEndpoints Sandbox ver3 only";
                   s.Version = "v3.0";
               };
           o.MinEndpointVersion = 3;
           o.MaxEndpointVersion = 3;
           o.ExcludeNonFastEndpoints = true;
       })

   //used for release versioning tests
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v0";
                                };
           o.ReleaseVersion = 0;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v1";
                                };
           o.ReleaseVersion = 1;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v2";
                                };
           o.ReleaseVersion = 2;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v3";
                                };
           o.ReleaseVersion = 3;
           o.ShowDeprecatedOps = true;
       });

var supportedCultures = new[] { new CultureInfo("en-US") };

var app = bld.Build();
app.UseRequestLocalization(
       new RequestLocalizationOptions
       {
           DefaultRequestCulture = new("en-US"),
           SupportedCultures = supportedCultures,
           SupportedUICultures = supportedCultures
       })
   .UseDefaultExceptionHandler()
   .UseResponseCaching()
   .UseRouting() //if using, this call must go before auth/cors/fastendpoints middleware
   .UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())
   .UseJwtRevocation<JwtBlacklistChecker>()
   .UseAuthentication()
   .UseAuthorization()
   .UseAntiforgeryFE(additionalContentTypes: ["application/json"])
   .UseOutputCache()
   .UseFastEndpoints(
       c =>
       {
           c.Validation.EnableDataAnnotationsSupport = true;

           c.Binding.UsePropertyNamingPolicy = true;
           c.Binding.ReflectionCache.AddFromWeb();
           c.Binding.ValueParserFor<Guid>(x => new(Guid.TryParse(x, out var res), res));

           c.Endpoints.RoutePrefix = "api";
           c.Endpoints.ShortNames = false;
           c.Endpoints.PrefixNameWithFirstTag = true;
           c.Endpoints.Filter = ep => ep.EndpointTags?.Contains("exclude") is not true;
           c.Endpoints.Configurator =
               ep =>
               {
                   ep.PreProcessor<GlobalStatePreProcessor>(Order.Before);
                   ep.PreProcessors(Order.Before, new AdminHeaderChecker());
                   if (ep.EndpointTags?.Contains("Orders") is true)
                       ep.Description(b => b.Produces<ErrorResponse>(400, "application/problem+json"));
               };

           c.Versioning.Prefix = "ver";

           c.Throttle.HeaderName = "X-Custom-Throttle-Header";
           c.Throttle.Message = "Custom Error Response";
       })
   .UseEndpoints(
       c => //this must go after usefastendpoints (only if using endpoints)
       {
           c.MapGet("test", () => "hello world!").WithTags("map-get");
           c.MapGet("test/{testId:int?}", (int? testId) => $"hello {testId}").WithTags("map-get");
       });

if (!app.Environment.IsProduction())
    app.UseSwaggerGen();

app.Services.RegisterGenericCommand(typeof(GenericCommand<>), typeof(GenericCommandHandler<>));
app.Services.RegisterGenericCommand(typeof(GenericNoResultCommand<>), typeof(GenericNoResultCommandHandler<>));
app.Services.RegisterGenericCommand<JobTestGenericCommand<SomeEvent>, JobTestGenericCommandHandler<SomeEvent>>();

app.MapHandlers(
    h =>
    {
        h.Register<VoidCommand, VoidCommandHandler>();
        h.Register<SomeCommand, SomeCommandHandler, string>();
        h.Register<EchoCommand, EchoCommandHandler, EchoCommand>();
        h.RegisterServerStream<StatusStreamCommand, StatusUpdateHandler, StatusUpdate>();
        h.RegisterClientStream<CurrentPosition, PositionProgressHandler, ProgressReport>();
        h.RegisterEventHub<TestEventQueue>();
    });

app.UseJobQueues(
    o =>
    {
        o.MaxConcurrency = 4;
        o.LimitsFor<JobTestCommand>(1, TimeSpan.FromSeconds(1));
        o.LimitsFor<JobCancelTestCommand>(100, TimeSpan.FromSeconds(60));
        o.StorageProbeDelay = TimeSpan.FromMilliseconds(100);
    });

var isTestHost = app.Services.CreateScope().ServiceProvider.GetService<IEmailService>() is not EmailService;

if (isTestHost && app.Environment.EnvironmentName != "Testing")
    throw new InvalidOperationException("TestFixture hasn't set the test environment correctly!");

app.Run();

namespace Web
{
    public class Program { }
}