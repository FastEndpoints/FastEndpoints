using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NativeAotCheckerTests;

public class StandaloneMessagingTests
{
    [Fact]
    public async Task Messaging_Only_Generated_Discovery_And_Inheritance()
    {
        var smoke = AppContext.TryGetSwitch("NativeAotTestMode", out var enabled) && enabled;
        if (smoke)
        {
#if NET11_0_OR_GREATER
            const string framework = "net11.0";
#else
            const string framework = "net10.0";
#endif
            var output = Path.Combine(AppContext.BaseDirectory, "messaging-aot");
            // Always publish: referenced-library changes are not covered by the web fixture's fingerprint.
            await Run("dotnet", "publish", ProjectPath(), "-c", "Release", "-f", framework, "-o", output);
            await Run(Path.Combine(output, "MessagingAotChecker" + (OperatingSystem.IsWindows() ? ".exe" : "")));
        }
        else
            await Run("dotnet", typeof(MessagingAotChecker.Program).Assembly.Location);
    }

    static string ProjectPath([CallerFilePath] string source = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../../../../TestHarness/MessagingAotChecker/MessagingAotChecker.csproj"));

    static async Task Run(string command, params string[] arguments)
    {
        var start = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            process.ExitCode.ShouldBe(0, output);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }
}
