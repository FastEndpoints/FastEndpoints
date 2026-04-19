namespace OpenApi.Kiota.Tests;

public sealed class CommandModeTests : IAsyncLifetime
{
    readonly string _artifactRoot = Path.Combine(Path.GetTempPath(), "fe-openapi-kiota-tests", Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_artifactRoot);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task export_openapi_json_mode_writes_expected_file_and_exits_cleanly()
    {
        var result = await RunHarnessAsync("exportopenapijson", "true");

        result.ExitCode.ShouldBe(0);

        var outputFile = Path.Combine(result.ArtifactRoot, "openapi-json", "kiota-spec.json");
        File.Exists(outputFile).ShouldBeTrue();

        var json = await File.ReadAllTextAsync(outputFile, TestContext.Current.CancellationToken);
        json.ShouldContain("\"openapi\"");
        json.ShouldContain("Kiota Harness");
    }

    [Fact]
    public async Task generate_api_clients_mode_writes_client_files_and_archive()
    {
        var result = await RunHarnessAsync("generateclients", "true");

        result.ExitCode.ShouldBe(0);

        var generatedClientDir = Path.Combine(result.ArtifactRoot, "generated-client");
        Directory.Exists(generatedClientDir).ShouldBeTrue();
        Directory.EnumerateFiles(generatedClientDir, "*.cs", SearchOption.AllDirectories).Any().ShouldBeTrue();

        var archivePath = Path.Combine(result.ArtifactRoot, "HarnessApiClient.zip");
        File.Exists(archivePath).ShouldBeTrue();

        using var archive = await ZipFile.OpenReadAsync(archivePath);
        archive.Entries.Any(e => e.FullName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    async Task<(int ExitCode, string ArtifactRoot)> RunHarnessAsync(string key, string value)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "TestHarness", "OpenApi.Kiota", "OpenApi.Kiota.csproj"));
        var proc = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        proc.ArgumentList.Add("run");
        proc.ArgumentList.Add("--no-build");
        proc.ArgumentList.Add("--project");
        proc.ArgumentList.Add(projectPath);
        proc.ArgumentList.Add("--");
        proc.ArgumentList.Add($"--{key}");
        proc.ArgumentList.Add(value);
        proc.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        proc.Environment["FE_KIOTA_ARTIFACT_ROOT"] = _artifactRoot;

        using var process = Process.Start(proc)!;
        var stdOut = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdErr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        if (process.ExitCode != 0)
        {
            var output = await stdOut;
            var error = await stdErr;

            throw new Xunit.Sdk.XunitException($"Harness exited with code {process.ExitCode}\nSTDOUT:\n{output}\nSTDERR:\n{error}");
        }

        return (process.ExitCode, _artifactRoot);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_artifactRoot))
            Directory.Delete(_artifactRoot, true);

        return ValueTask.CompletedTask;
    }
}