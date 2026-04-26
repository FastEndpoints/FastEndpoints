using System.Globalization;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;
using TestCases.EventHandlingTest;
using TestCases.EventQueueTest;
using TestCases.GlobalGenericProcessorTest;
using TestCases.JobQueueTest;
using TestCases.KeyedServicesTests;
using TestCases.MetadataRegistrationTest;
using TestCases.ProcessorStateTest;
using TestCases.ServerStreamingTest;
using TestCases.UnitTestConcurrencyTest;
using TestCases.X402;
using Web;
using Web.PipelineBehaviors.PreProcessors;
using Web.Services;

Func<EndpointDefinition, bool> excludeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
Func<EndpointDefinition, bool> includeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is true;
Func<EndpointDefinition, bool> includeSwaggerReview = ep => ep.EndpointTags?.Contains("swagger_review") is true;

var bld = WebApplication.CreateBuilder(args);
bld.AddHandlerServer();
bld.Services
   .AddCors()
   .AddOutputCache()
   .AddIdempotency()
   .AddResponseCaching()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddX402()
   .AddAuthenticationJwtBearer(s => s.SigningKey = bld.Configuration["TokenKey"]!)
   .AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)))
   .AddKeyedTransient<IKeyedService>("AAA", (_, _) => new MyKeyedService("AAA"))
   .AddKeyedTransient<IKeyedService>("BBB", (_, _) => new MyKeyedService("BBB"))
   .AddScoped<IEmailService, EmailService>()
   .AddSingleton(new SingltonSVC(0))
   .AddJobQueues<Job, JobStorage>()
   .RegisterServicesFromWeb()
   .AddAntiforgery()
   .OpenApiDocument(
       o =>
       {
           o.EndpointFilter = excludeReleaseVersioning;
           o.DocumentName = "Initial Release";
           o.Title = "Web API";
           o.Version = "v0.0";
           o.TagCase = TagCase.TitleCase;
           o.TagStripSymbols = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.EndpointFilter = excludeReleaseVersioning;
           o.DocumentName = "Release 1.0";
           o.Title = "Web API";
           o.Version = "v1.0";
           o.AddAuth(
               "ApiKey",
               new()
               {
                   Name = "api_key",
                   In = ParameterLocation.Header,
                   Type = SecuritySchemeType.ApiKey
               });
           o.MaxEndpointVersion = 1;
           o.TagStripSymbols = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.EndpointFilter = excludeReleaseVersioning;
           o.DocumentName = "Release 2.0";
           o.Title = "FastEndpoints Sandbox";
           o.Version = "v2.0";
           o.MaxEndpointVersion = 2;
           o.ShowDeprecatedOps = true;
           o.TagStripSymbols = true;
       })
   .OpenApiDocument(
       o => //only ver3 & only FastEndpoints
       {
           o.EndpointFilter = excludeReleaseVersioning;
           o.DocumentName = "Release 3.0";
           o.Title = "FastEndpoints Sandbox ver3 only";
           o.Version = "v3.0";
           o.MinEndpointVersion = 3;
           o.MaxEndpointVersion = 3;
           o.ExcludeNonFastEndpoints = true;
       })

   //used for release versioning tests
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeReleaseVersioning;
           o.Title = "Web API";
           o.DocumentName = "ReleaseVersioning - v0";
           o.ReleaseVersion = 0;
           o.ShowDeprecatedOps = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeReleaseVersioning;
           o.Title = "Web API";
           o.DocumentName = "ReleaseVersioning - v1";
           o.ReleaseVersion = 1;
           o.ShowDeprecatedOps = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeReleaseVersioning;
           o.Title = "Web API";
           o.DocumentName = "ReleaseVersioning - v2";
           o.ReleaseVersion = 2;
           o.ShowDeprecatedOps = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeReleaseVersioning;
           o.Title = "Web API";
           o.DocumentName = "ReleaseVersioning - v3";
           o.ReleaseVersion = 3;
           o.ShowDeprecatedOps = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeSwaggerReview;
           o.Title = "Web API";
           o.DocumentName = "Swagger Review";
           o.TagStripSymbols = true;
       })
   .OpenApiDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = includeSwaggerReview;
           o.Title = "Web API";
           o.DocumentName = "Swagger Review Empty Schema";
           o.TagStripSymbols = true;
       });

if (bld.Environment.EnvironmentName == "Testing")
    bld.Services.AddSingleton<IX402FacilitatorClient, FakeFacilitatorClient>();

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
   .UseX402(
       o =>
       {
           o.FacilitatorUrl = "https://fake-facilitator.test/";
           o.Defaults.Network = "eip155:84532";
           o.Defaults.PayTo = "0xdefault";
           o.Defaults.Asset = "0xasset";
           o.Defaults.MimeType = "application/json";
       })
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
                   ep.PreProcessors(Order.Before, typeof(GlobalGenericPreProcessor<>));
                   ep.PostProcessors(Order.After, typeof(GlobalGenericPostProcessor<,>));
                   ep.PreProcessor<GlobalStatePreProcessor>(Order.Before);
                   ep.PreProcessors(Order.Before, new AdminHeaderChecker());
                   if (ep.EndpointTags?.Contains("Orders") is true)
                       ep.Description(b => b.Produces<ErrorResponse>(400, "application/problem+json"));
                   if (ep.EndpointMetadata?.OfType<SomeObject>().Any(s => s.Yes) is true)
                       ep.AllowAnonymous();
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
           c.MapGet("filtered-shared-path", () => "get").WithTags("exclude");
           c.MapPost("filtered-shared-path", () => TypedResults.Ok("post")).WithTags("shared-path");
       });

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(
        o =>
        {
            o.AddDocuments("Initial Release", "Release 1.0", "Release 2.0", "Release 3.0");
            o.OperationTitleSource = OperationTitleSource.Path;
        });
}

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
        h.RegisterEventHub<MyEvent>();
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