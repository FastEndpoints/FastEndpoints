using FastEndpoints.Security;
using NativeAotChecker;
using FastEndpoints.Swagger;
using NativeAotChecker.Endpoints;
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
       });

#if !RELEASE //exclude nswag from aot build
bld.Services.SwaggerDocument(o => o.DocumentSettings = s => s.DocumentName = "v1");
#endif

var app = bld.Build();
app.UseStaticFiles();
app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Binding.ReflectionCache.AddFromNativeAotChecker();
           c.Endpoints.Configurator = ep => { ep.PreProcessors(Order.Before, typeof(OpenGenericGlobalPreProcessor<>)); };
       });

await app.ExportSwaggerDocsAndExitAsync("", "v1");

app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.UseOpenApi(c => c.Path = "/openapi/{documentName}.json");
app.MapScalarApiReference(o => o.AddDocument("v1"));
app.Run();