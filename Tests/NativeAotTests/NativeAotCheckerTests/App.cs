using NativeAotCheckerTests;

[assembly: AssemblyFixture(typeof(App))]

namespace NativeAotCheckerTests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

public class App : IAsyncLifetime
{
    private Process? _apiProcess;
    private readonly string _port = "5050";
    public string BaseUrl => $"http://localhost:{_port}";
    public HttpClient Client { get; } = new();

    private readonly string _projectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "NativeAotChecker");
    private string _exePath = "";

    public async ValueTask InitializeAsync()
    {
        var publishDir = Path.Combine(_projectPath, "aot");
        var rid = RuntimeInformation.RuntimeIdentifier;
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NativeAotChecker.exe" : "NativeAotChecker";
        _exePath = Path.Combine(publishDir, exeName);

        await RunPublishAsync(publishDir, rid);
        StartApiProcess();
        await WaitForApiReadyAsync();
    }

    private async Task RunPublishAsync(string outputDir, string rid)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{_projectPath}\" -c Release -r {rid} -o \"{outputDir}\" /p:PublishAot=true",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };

        using var process = Process.Start(startInfo) ?? throw new("Failed to start dotnet publish");
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();

            throw new($"AOT Publish failed: {error}");
        }
    }

    private void StartApiProcess()
    {
        _apiProcess = new()
        {
            StartInfo = new()
            {
                FileName = _exePath,
                Arguments = $"--urls={BaseUrl}",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized
            }
        };
        _apiProcess.Start();
    }

    private async Task WaitForApiReadyAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            try
            {
                var response = await Client.GetAsync($"{BaseUrl}/healthy");

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                /* Polling */
            }
            await Task.Delay(500);
        }

        throw new("AOT API failed to respond in time.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_apiProcess != null && !_apiProcess.HasExited)
        {
            _apiProcess.Kill();
            await _apiProcess.WaitForExitAsync();
        }
        _apiProcess?.Dispose();
        Client.Dispose();
    }
}