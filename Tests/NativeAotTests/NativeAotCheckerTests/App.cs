using System.Text.Json;
using NativeAotCheckerTests;

[assembly: AssemblyFixture(typeof(App))]

namespace NativeAotCheckerTests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

// NOTE:
//  native aot executables cannot be debugged (in rider).
//  cannot be tested via WAF either. as of jan-2026.
//  current testing flow is as follows:
//  - publish a native aot executable with `dotnet publish /p:PublishAot=true`
//  - start the built app
//  - wait for it to respond on /healthy endpoint
//  - run test suit via http client (with routeless testing helpers from fastendpoints)
//  - clean up on completion

public class App : IAsyncLifetime
{
    public HttpClient Client { get; } = new() { BaseAddress = new(_baseUrl) };

    private static readonly string _port = "5050";
    private static readonly string _baseUrl = $"http://localhost:{_port}";
    private static readonly string _appName = "NativeAotChecker";
    private static readonly string _projectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", _appName);
    private static string _exePath = "";
    private static Process? _apiProcess;

    public async ValueTask InitializeAsync()
    {
        //make the aot app and the test helpers use the same serializer settings
        var cfg = new Config();
        cfg.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        var publishDir = Path.Combine(_projectPath, "aot");
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{_appName}.exe" : _appName;
        _exePath = Path.Combine(publishDir, exeName);

        await RunPublishAsync(publishDir, RuntimeInformation.RuntimeIdentifier);
        StartApiProcess();
        await WaitForApiReadyAsync();
    }

    private static async Task RunPublishAsync(string outputDir, string rid)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{_projectPath}\" -c Release -r {rid} -o \"{outputDir}\"",
            WindowStyle = ProcessWindowStyle.Normal,
            UseShellExecute = true,
            RedirectStandardError = false
        };

        using var process = Process.Start(startInfo) ?? throw new("Failed to start dotnet publish");
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();

            throw new($"AOT Publish failed: {error}");
        }
    }

    private static void StartApiProcess()
    {
        _apiProcess = new()
        {
            StartInfo = new()
            {
                FileName = _exePath,
                Arguments = $"--urls={_baseUrl}",
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
                var response = await Client.GetAsync($"{_baseUrl}/healthy");

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                //do nothing
            }
            await Task.Delay(500);
        }

        throw new("AOT API failed to respond in time.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_apiProcess is { HasExited: false })
        {
            _apiProcess.Kill();
            await _apiProcess.WaitForExitAsync();
        }
        _apiProcess?.Dispose();
        Client.Dispose();

        //fire off a non-aot build to avoid ide errors
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build",
            WindowStyle = ProcessWindowStyle.Minimized,
            UseShellExecute = true,
            RedirectStandardError = false,
            WorkingDirectory = _projectPath
        };

        using var process = Process.Start(startInfo) ?? throw new("Failed to run dotnet clean");
        await process.WaitForExitAsync();
    }
}