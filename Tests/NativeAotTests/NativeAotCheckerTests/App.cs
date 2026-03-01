using System.Runtime.InteropServices;
using System.Text.Json;
using FastEndpoints.Testing;

namespace NativeAotCheckerTests;

public class App : AppFixture<Program>
{
    private static readonly string _appName = "NativeAotChecker";

    protected override void ConfigureAotTarget(AotTargetOptions options)
    {
        options.ExePath = GetExePath();
        options.BaseUrl = "http://localhost:5000";

        static string GetExePath()
        {
            var testDir = Path.GetDirectoryName(typeof(App).Assembly.Location)!;
            var aotDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "aot"));
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{_appName}.exe" : _appName;

            return Path.Combine(aotDir, exeName); // path to aot binary: Tests/NativeAotTests/NativeAotCheckerTests/aot/NativeAotChecker.exe
        }
    }

    protected override ValueTask SetupAsync()
    {
        //make the aot app and the test helpers use the same serializer settings
        var cfg = new Config();
        cfg.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        return ValueTask.CompletedTask;
    }
}