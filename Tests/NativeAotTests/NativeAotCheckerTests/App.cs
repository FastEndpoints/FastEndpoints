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
        options.BuildTimeoutMinutes = 15;
        options.ReadyTimeoutSeconds = 60;
    }
}
