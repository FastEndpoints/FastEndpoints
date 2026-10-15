using System.Text.Json;

namespace NativeAotCheckerTests;

public class App : AppFixture<Program>
{
    public App()
    {
        // keep test-helper serialization aligned with the app before the shared serializer options become read-only
        new Config().Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    }

    protected override async ValueTask ConfigureAotTargetAsync(AotTargetOptions options)
    {
#if NET11_0_OR_GREATER
        const string framework = "net11.0";
#else
        const string framework = "net10.0";
#endif
        options.AdditionalPublishArguments = $"-f {framework}";
        options.AotBuildOutputPath = Path.Combine(AppContext.BaseDirectory, "aot");
        options.BuildTimeoutMinutes = 15;
        options.ReadyTimeoutSeconds = 60;
    }
}
