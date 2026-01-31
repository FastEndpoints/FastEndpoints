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
bld.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = bld.Build();

// Register generic command handler for AOT - PR2 needs to make this AOT-compatible
// This registers the open generic types - when executed, InitGenericHandler uses MakeGenericType
// to create the closed types (e.g., GenericWrapperCommandHandler<string>), which fails in AOT
app.Services.RegisterGenericCommand(typeof(GenericWrapperCommand<>), typeof(GenericWrapperCommandHandler<>));

app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(c => c.Binding.ReflectionCache.AddFromNativeAotChecker());
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Run();

//needed by the hidden /_test_url_cache_ endpoint
[JsonSerializable(typeof(IEnumerable<string>))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }