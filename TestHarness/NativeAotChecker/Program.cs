using System.Text.Json;
using FastEndpoints.OpenApi;
using FastEndpoints.Security;
using NativeAotChecker;
using NativeAotChecker.Endpoints.CommandRules;
using NativeAotChecker.Endpoints.Commands;
using NativeAotChecker.Endpoints.Jobs;
using NativeAotChecker.Endpoints.Processors;
using Scalar.AspNetCore;

var bld = WebApplication.CreateSlimBuilder(args);
bld.Services
   .AddAuthenticationJwtBearer(o => o.SigningKey = bld.Configuration["Jwt-Secret"])
   .AddAuthorization()
   .AddFastEndpoints(DiscoveredTypes.All, GenericProcessorTypes.All)
   .AddCommandRules(
       o =>
       {
           o.MatchMode = CommandRuleMatchMode.All;
           o.UnhandledBehavior = UnhandledRuleBehavior.NoOp;
           o.DefaultMode = CommandDispatchMode.ExecuteNow;
           o.FailureBehavior = CommandDispatchFailureBehavior.Continue;
       })
   .AddCommandRule<CommandRulesRequest, FirstCommandRule>()
   .AddCommandRule<CommandRulesRequest, SecondCommandRule>()
   .AddCommandRule<CommandRulesRequest, ExecuteNowCommandRule>()
   .AddCommandRule<CommandRulesRequest, QueueJobCommandRule>()
   .AddCommandRule<CommandRulesRequest, UnsupportedCommandRule>()
   .AddJobQueues<Job, JobStorage>()
   .AddCommandMiddleware(
       c =>
       {
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, FirstMiddleware>();
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, SecondMiddleware<MiddlewareTestCmd, MiddlewareTestResult>>();
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, ThirdMiddleware<MiddlewareTestCmd, MiddlewareTestResult>>();
       })
   .AddStreamCommandMiddleware(c => c.Register<StreamNumbersWithMiddlewareAotCommand, int, StreamNumbersAotMiddleware>())
   .OpenApiDocument(o => o.DocumentName = "v1");

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.UseStaticFiles()
   .UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Endpoints.Warmup();
           c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
           c.Serializer.Options.AddSerializerContextsFromNativeAotChecker();
           c.Binding.ReflectionCache.AddFromNativeAotChecker();
           c.Endpoints.Configurator = ep => { ep.PreProcessors(Order.Before, typeof(OpenGenericGlobalPreProcessor<>)); };
       });

await app.ExportOpenApiDocsAndExitAsync("v1");

app.MapScalarApiReference(o => o.AddDocument("v1"));
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Services.RegisterGenericCommand<AotGenericCommand<ProductData>, AotGenericResult<ProductData>, AotGenericCommandHandler<ProductData>>();
app.Services.RegisterStreamCommand<StreamNumbersAotCommand, int, StreamNumbersAotCommandHandler>();
app.Services.RegisterStreamCommand<StreamNumbersWithMiddlewareAotCommand, int, StreamNumbersWithMiddlewareAotCommandHandler>();
app.Run();