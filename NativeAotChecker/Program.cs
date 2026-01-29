using System.Text.Json.Serialization;
using NativeAotChecker;
using NativeAotChecker.Endpoints;

var bld = WebApplication.CreateSlimBuilder(args);
bld.Services
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddJobQueues<Job, JobStorage>();
bld.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.UseFastEndpoints(c => c.Binding.ReflectionCache.AddFromNativeAotChecker());
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Run();

//needed by the hidden /_test_url_cache_ endpoint
[JsonSerializable(typeof(IEnumerable<string>))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }