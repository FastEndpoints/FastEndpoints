using System.Text.Json;
using FastEndpoints.Security;
using NativeAotChecker;
using FastEndpoints.Swagger;
using NativeAotChecker.Endpoints.Commands;
using NativeAotChecker.Endpoints.Jobs;
using NativeAotChecker.Endpoints.Processors;
using Scalar.AspNetCore;

var bld = WebApplication.CreateSlimBuilder(args);
bld.Services
   .AddAuthenticationJwtBearer(o => o.SigningKey = bld.Configuration["Jwt-Secret"])
   .AddAuthorization()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = [..DiscoveredTypes.All, ..GenericProcessorTypes.All])
   .AddJobQueues<Job, JobStorage>()
   .AddCommandMiddleware(
       c =>
       {
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, FirstMiddleware>();
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, SecondMiddleware<MiddlewareTestCmd, MiddlewareTestResult>>();
           c.Register<MiddlewareTestCmd, MiddlewareTestResult, ThirdMiddleware<MiddlewareTestCmd, MiddlewareTestResult>>();
       })
   .SwaggerDocument(o => o.DocumentSettings = s => s.DocumentName = "v1");

var app = bld.Build();
app.UseStaticFiles();
app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
           c.Serializer.Options.AddSerializerContextsFromNativeAotChecker();
           c.Binding.ReflectionCache.AddFromNativeAotChecker();
           c.Endpoints.Configurator = ep => { ep.PreProcessors(Order.Before, typeof(OpenGenericGlobalPreProcessor<>)); };
       });

await app.ExportSwaggerDocsAndExitAsync("v1");

app.UseOpenApi(c => c.Path = "/openapi/{documentName}.json");
app.MapScalarApiReference(o => o.AddDocument("v1"));

app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Services.RegisterGenericCommand<AotGenericCommand<ProductData>, AotGenericResult<ProductData>, AotGenericCommandHandler<ProductData>>();
app.Run();