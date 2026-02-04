using System.Text.Json;
using NativeAotCheckerTests;

[assembly: AssemblyFixture(typeof(App))]

namespace NativeAotCheckerTests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

// NOTE:
//  native aot executables cannot be debugged (in rider).
//  cannot be tested via WAF either. as of jan-2026.
//  current testing flow is as follows:
//  - aot binary is built incrementally via msbuild target before test compilation (only when NativeAotChecker source files are newer than the binary)
//  - start the built app
//  - wait for it to respond on /healthy endpoint
//  - run test suit via http client (with routeless testing helpers from fastendpoints)

public class App : IAsyncLifetime
{
    public HttpClient Client { get; } = new() { BaseAddress = new(_baseUrl) };

    private static readonly string _port = "5000";
    private static readonly string _baseUrl = $"http://localhost:{_port}";
    private static readonly string _appName = "NativeAotChecker";
    private static readonly string _exePath = GetExePath();
    private static readonly string _exeDir = Path.GetDirectoryName(_exePath)!;
    private static Process? _apiProcess;
    private static readonly StringBuilder _processOutput = new();

    private static string GetExePath()
    {
        var testDir = Path.GetDirectoryName(typeof(App).Assembly.Location)!;
        var aotDir = Path.Combine(testDir, "..", "..", "..", "aot");
        aotDir = Path.GetFullPath(aotDir);
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{_appName}.exe" : _appName;

        return Path.Combine(aotDir, exeName); // path to aot binary: Tests/NativeAotTests/NativeAotCheckerTests/aot/NativeAotChecker.exe
    }

    public async ValueTask InitializeAsync()
    {
        //make the aot app and the test helpers use the same serializer settings
        var cfg = new Config();
        cfg.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        if (!File.Exists(_exePath))
            throw new FileNotFoundException("AOT executable not found. Run build to generate it.", _exePath);

        StartApiProcess();
        await WaitForApiReadyAsync();
    }

    private static void StartApiProcess()
    {
        _processOutput.Clear();
        _apiProcess = new()
        {
            StartInfo = new()
            {
                FileName = _exePath,
                Arguments = $"--urls={_baseUrl}",
                WorkingDirectory = _exeDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Normal
            }
        };
        _apiProcess.OutputDataReceived += (_, e) =>
                                          {
                                              if (e.Data != null)
                                                  _processOutput.AppendLine(e.Data);
                                          };
        _apiProcess.ErrorDataReceived += (_, e) =>
                                         {
                                             if (e.Data != null)
                                                 _processOutput.AppendLine(e.Data);
                                         };
        _apiProcess.Start();
        _apiProcess.BeginErrorReadLine();
        _apiProcess.BeginOutputReadLine();
    }

    private async Task WaitForApiReadyAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (_apiProcess?.HasExited == true)
            {
                var output = _processOutput.ToString();

                throw new($"AOT process exited unexpectedly with code {_apiProcess.ExitCode}.\n\nProcess output:\n{output}");
            }

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

        var finalOutput = _processOutput.ToString();

        throw new($"AOT API failed to respond in time.\n\nProcess output:\n{finalOutput}");
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
    }
}