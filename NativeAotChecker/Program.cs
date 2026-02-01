using FastEndpoints.Security;
using NativeAotChecker;
using NativeAotChecker.Endpoints;

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

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(
       c =>
       {
           c.Binding.ReflectionCache.AddFromNativeAotChecker();
           c.Endpoints.Configurator = ep => { ep.PreProcessors(Order.Before, typeof(OpenGenericGlobalPreProcessor<>)); };
       });
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Run();