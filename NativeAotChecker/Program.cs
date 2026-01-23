var bld = WebApplication.CreateSlimBuilder(args);

bld.Services.ConfigureHttpJsonOptions(
    options =>
    {
        //options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    });

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.MapGet("hello", () => "hello world!");

app.Run();