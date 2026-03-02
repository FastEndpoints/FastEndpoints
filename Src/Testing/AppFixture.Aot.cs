using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace FastEndpoints.Testing;

public abstract partial class AppFixture<TProgram>
{
    AotSharedState? _aotState;
    Type? _aotCacheKey;
    static readonly ConcurrentDictionary<Type, AsyncLazy<AotSharedState>> _aotCache = new();

    // ReSharper disable once UnusedParameter.Global
    /// <summary>
    /// override this method to configure the native aot target settings.
    /// <para>
    /// NOTE: only the first fixture class per <typeparamref name="TProgram" /> type will have this method invoked. all fixtures sharing the same
    /// <typeparamref name="TProgram" /> will share a single AOT process instance.
    /// </para>
    /// </summary>
    protected virtual ValueTask ConfigureAotTargetAsync(AotTargetOptions options)
        => ValueTask.CompletedTask;

    async ValueTask InitializeAotAsync()
    {
        _aotCacheKey = typeof(TProgram);
        _aotState = await _aotCache.GetOrAdd(
                        _aotCacheKey,
                        _ => new(
                            async () =>
                            {
                                var opts = new AotTargetOptions();
                                await ConfigureAotTargetAsync(opts);

                                return await StartAotAsync(ResolveAotPaths(opts), opts);
                            }));
        Client = CreateAotClient();
    }

    static async Task<AotSharedState> StartAotAsync((string ExePath, string AotProjectPath, string AotBuildOutputPath) resolved, AotTargetOptions o)
    {
        await BuildAotAsync(resolved.AotProjectPath, resolved.AotBuildOutputPath, o.BuildTimeoutMinutes);

        if (!File.Exists(resolved.ExePath))
        {
            throw new FileNotFoundException(
                $"AOT publish succeeded but the executable was not found at the expected path: {resolved.ExePath}. " +
                "Verify the output path and executable name configuration in ConfigureAotTargetAsync().",
                resolved.ExePath);
        }

        if (o.BaseUrl is null)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            o.BaseUrl = $"http://localhost:{port}";
        }

        var baseUri = new Uri(o.BaseUrl, UriKind.Absolute);
        var workingDir = Path.GetDirectoryName(resolved.ExePath)!;

        var state = new AotSharedState(baseUri, resolved.ExePath, workingDir, o);

        try
        {
            state.StartProcess();
            await state.WaitForReadyAsync();
        }
        catch
        {
            await state.DisposeAsync();

            throw;
        }

        return state;
    }

    static (string ExePath, string AotProjectPath, string AotBuildOutputPath) ResolveAotPaths(AotTargetOptions opts)
    {
        var testProjectDir = AppContext.BaseDirectory;
        while (testProjectDir != null && Directory.GetFiles(testProjectDir, "*.csproj").Length == 0)
            testProjectDir = Path.GetDirectoryName(testProjectDir);

        if (testProjectDir == null)
            throw new InvalidOperationException("Could not locate test project directory.");

        var aotProjectPath = opts.PathToTargetAotProject ?? FindAotProjectFromTProgram(testProjectDir);
        var exeName = Path.GetFileNameWithoutExtension(aotProjectPath);
        var buildOutputPath = opts.AotBuildOutputPath ?? Path.Combine(testProjectDir, "bin", "aot", exeName);

        var exePath = opts.PathToAotExecutable;

        if (exePath is null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                exeName += ".exe";

            exePath = Path.Combine(buildOutputPath, exeName);
        }

        return (exePath, aotProjectPath, buildOutputPath);
    }

    static string FindAotProjectFromTProgram(string testProjectDir)
    {
        var tProgram = typeof(TProgram);
        var assemblyName = tProgram.Assembly.GetName().Name;

        if (assemblyName == null)
            throw new InvalidOperationException("Could not get assembly name from TProgram.");

        var csprojName = $"{assemblyName}.csproj";

        var currentDir = new DirectoryInfo(testProjectDir);
        DirectoryInfo? solutionDir = null;

        while (currentDir != null)
        {
            if (currentDir.GetFiles("*.sln").Length > 0 || currentDir.GetFiles("*.slnx").Length > 0)
            {
                solutionDir = currentDir;

                break;
            }
            currentDir = currentDir.Parent;
        }

        solutionDir ??= new DirectoryInfo(testProjectDir).Parent ?? new DirectoryInfo(testProjectDir);

        // search the solution dir and up to 5 levels deep (covers sibling projects and nested layouts)
        var matches = Directory.GetFiles(
            solutionDir.FullName,
            csprojName,
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 5
            });

        if (matches.Length > 0)
            return matches[0];

        throw new InvalidOperationException($"Could not auto-detect AOT project for {assemblyName}. Please set 'PathToTargetAotProject' explicitly in ConfigureAotTargetAsync().");
    }

    static async Task BuildAotAsync(string aotProjectPath, string outputPath, int buildTimeoutMinutes)
    {
        var projectDir = Path.GetDirectoryName(aotProjectPath)!;

        Directory.CreateDirectory(outputPath);

        var buildOutput = new ConcurrentQueue<string>();

        using var process = new Process();
        process.StartInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"publish \"{aotProjectPath}\" -c Release -o \"{outputPath}\"",
            WorkingDirectory = projectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        process.OutputDataReceived += (_, e) =>
                                      {
                                          if (e.Data is not null)
                                          {
                                              buildOutput.Enqueue(e.Data);
                                              Console.Error.WriteLine(e.Data);
                                          }
                                      };
        process.ErrorDataReceived += (_, e) =>
                                     {
                                         if (e.Data is not null)
                                         {
                                             buildOutput.Enqueue(e.Data);
                                             Console.Error.WriteLine(e.Data);
                                         }
                                     };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var effectiveTimeout = buildTimeoutMinutes > 0 ? buildTimeoutMinutes : 5;
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(effectiveTimeout));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            throw new TimeoutException(
                $"AOT build timed out after {effectiveTimeout} minute(s). Consider increasing BuildTimeoutMinutes.\n\n" +
                $"Output:\n{string.Join(Environment.NewLine, buildOutput)}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"AOT build failed with exit code {process.ExitCode}.\n\n" +
                $"Output:\n{string.Join(Environment.NewLine, buildOutput)}");
        }
    }

    HttpClient CreateAotClient()
        => new() { BaseAddress = _aotState!.BaseAddress };

    /// <summary>
    /// configure settings for the target native aot application. all properties are optional. you can override the defaults in case the auto-detection fails.
    /// </summary>
    protected sealed class AotTargetOptions
    {
        /// <summary>
        /// the full path to the published native aot executable (including the file name). auto-detected from TProgram if not set.
        /// </summary>
        public string? PathToAotExecutable { get; set; }

        /// <summary>
        /// full path to the aot project .csproj to build. auto-detected from TProgram if not set.
        /// </summary>
        public string? PathToTargetAotProject { get; set; }

        /// <summary>
        /// build output path for the aot executable. defaults to "bin/aot" relative to test project.
        /// </summary>
        public string? AotBuildOutputPath { get; set; }

        /// <summary>
        /// the base address (host/port) to bind the aot app instance to. auto-generated with random port if not set.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// the relative health endpoint path used to detect readiness (default: /healthy).
        /// </summary>
        public string? HealthEndpointPath { get; set; } = "/healthy";

        /// <summary>
        /// maximum number of seconds to wait for the target aot app to become ready (default: 15).
        /// </summary>
        public int ReadyTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// maximum number of minutes to wait for the aot publish/build to complete (default: 5).
        /// </summary>
        public int BuildTimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// additional environment variables to pass to the aot app instance.
        /// <para>by default <c>ASPNETCORE_ENVIRONMENT</c> is set to <c>Testing</c></para>
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; } = new()
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Testing"
        };
    }

    sealed class AotSharedState(Uri baseAddress, string exePath, string workingDir, AotTargetOptions o)
    {
        readonly ConcurrentQueue<string> _processOutput = new();
        Process? _process;

        public Uri BaseAddress { get; } = baseAddress;

        readonly string _healthPath = string.IsNullOrWhiteSpace(o.HealthEndpointPath) ? "/healthy" : o.HealthEndpointPath;
        readonly int _readyTimeoutSeconds = o.ReadyTimeoutSeconds > 0 ? o.ReadyTimeoutSeconds : 15;

        public void StartProcess()
        {
            _process = new()
            {
                StartInfo = new()
                {
                    FileName = exePath,
                    Arguments = $"--urls={BaseAddress}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Normal
                }
            };

            foreach (var (k, v) in o.EnvironmentVariables)
                _process.StartInfo.EnvironmentVariables[k] = v;

            _process.OutputDataReceived += (_, e) =>
                                           {
                                               if (e.Data is not null)
                                                   _processOutput.Enqueue(e.Data);
                                           };
            _process.ErrorDataReceived += (_, e) =>
                                          {
                                              if (e.Data is not null)
                                                  _processOutput.Enqueue(e.Data);
                                          };
            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                                                   {
                                                       try
                                                       {
                                                           _process.Kill(entireProcessTree: true);
                                                       }
                                                       catch
                                                       {
                                                           // ignore
                                                       }
                                                   };
        }

        public async Task WaitForReadyAsync()
        {
            using var client = new HttpClient();
            client.BaseAddress = BaseAddress;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(_readyTimeoutSeconds))
            {
                if (_process?.HasExited == true)
                {
                    var output = string.Join(Environment.NewLine, _processOutput);

                    throw new InvalidOperationException($"AOT process exited unexpectedly with code {_process.ExitCode}.\n\nProcess output:\n{output}");
                }

                try
                {
                    var response = await client.GetAsync(_healthPath);

                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch
                {
                    // do nothing
                }

                await Task.Delay(500);
            }

            var finalOutput = string.Join(Environment.NewLine, _processOutput);

            throw new InvalidOperationException($"AOT API failed to respond in time.\n\nProcess output:\n{finalOutput}");
        }

        public async ValueTask DisposeAsync()
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                try
                {
                    await _process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
            }
            _process?.Dispose();
        }
    }
}