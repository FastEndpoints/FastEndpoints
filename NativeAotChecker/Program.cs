using System.Text.Json.Serialization;
using FastEndpoints.Security;
using NativeAotChecker;
using NativeAotChecker.Endpoints;

var bld = WebApplication.CreateSlimBuilder(args);
bld.Services
   .AddAuthenticationJwtBearer(o => o.SigningKey = bld.Configuration["Jwt-Secret"])
   .AddAuthorization()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddJobQueues<Job, JobStorage>();

// Register DI services for testing
bld.Services.AddScoped<IScopedCounter, ScopedCounter>();
bld.Services.AddSingleton<ISingletonService, SingletonService>();
bld.Services.AddTransient<ITransientService, TransientService>();
bld.Services.AddScoped<IAotTestService, AotTestService>();
bld.Services.AddScoped<IPropertyInjectedService, PropertyInjectedService>();

bld.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(c => c.Binding.ReflectionCache.AddFromNativeAotChecker());
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Run();

//needed by the hidden /_test_url_cache_ endpoint
[JsonSerializable(typeof(IEnumerable<string>))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }