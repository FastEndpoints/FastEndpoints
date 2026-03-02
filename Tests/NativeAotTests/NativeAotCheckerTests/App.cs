using System.Text.Json;

namespace NativeAotCheckerTests;

public class App : AppFixture<Program>
{
    protected override async ValueTask ConfigureAotTargetAsync(AotTargetOptions options)
    {
        options.BuildTimeoutMinutes = 15;
        options.ReadyTimeoutSeconds = 60;
    }

    protected override ValueTask SetupAsync()
    {
        //make the aot app and the test helpers use the same serializer settings
        var cfg = new Config();
        cfg.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        return ValueTask.CompletedTask;
    }
}