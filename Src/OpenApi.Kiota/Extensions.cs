using System.IO.Compression;
using System.Text;
using Kiota.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi.Kiota;

#pragma warning disable VSTHRD200

public static class Extensions
{
    const string TempFolder = "KiotaClientGen";
    const string ApiClientGenerationKey = "generateclients";
    const string OpenApiJsonExportKey = "exportopenapijson";

    /// <summary>
    /// registers an endpoint that provides a download of the api client zip file for a given client generation configuration.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="route">the route to register</param>
    /// <param name="config">client generation configuration</param>
    /// <param name="options">endpoint options</param>
    public static IEndpointRouteBuilder MapApiClientEndpoint(this IEndpointRouteBuilder builder,
                                                             string route,
                                                             Action<ClientGenConfig> config,
                                                             Action<RouteHandlerBuilder>? options = null)
    {
        var b = builder.MapGet(
            route,
            async (IHost app, HttpContext httpCtx, CancellationToken ct) =>
            {
                var endpointRoot = GenerationWorkspace.CreateRootPath();
                var cfg = new ClientGenConfig { OutputPath = Path.Combine(endpointRoot, "client") };
                config(cfg);
                cfg.OutputPath = Path.Combine(endpointRoot, "client");
                cfg.CreateZipArchive = true;
                cfg.ZipOutputFile = Path.Combine(endpointRoot, $"{cfg.ClientClassName}.zip");

                try
                {
                    await GenerateClient(app, cfg, ct);

                    var zipFile = Path.GetFullPath(cfg.ZipOutputFile);
                    await Results.File(
                                     path: zipFile,
                                     contentType: "application/octet-stream",
                                     enableRangeProcessing: true,
                                     fileDownloadName: Path.GetFileName(zipFile))
                                 .ExecuteAsync(httpCtx);
                }
                finally
                {
                    SafeDeleteDirectory(endpointRoot);
                }
            });

        options?.Invoke(b);

        return builder;
    }

    extension(IHost app)
    {
        /// <summary>
        /// generates api clients based on supplied configurations and saves them to disk if the application is run with the commandline argument
        /// '<c>--generateclients true</c>' and exits the program with a zero exit code.
        /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
        /// </summary>
        /// <param name="configs">client generation configurations</param>
        public Task GenerateApiClientsAndExitAsync(params Action<ClientGenConfig>[] configs)
            => app.GenerateApiClientsAndExitAsync(CancellationToken.None, configs);

        /// <summary>
        /// generates api clients based on supplied configurations and saves them to disk if the application is run with the commandline argument
        /// '<c>--generateclients true</c>' and exits the program with a zero exit code.
        /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
        /// </summary>
        /// <param name="ct">cancellation token</param>
        /// <param name="configs">client generation configurations</param>
        public Task GenerateApiClientsAndExitAsync(CancellationToken ct, params Action<ClientGenConfig>[] configs)
            => app.RunGenerationModeAsync(
                app.IsApiClientGenerationMode(),
                ct,
                async cnTkn =>
                {
                    foreach (var config in configs)
                    {
                        var cfg = new ClientGenConfig();
                        config(cfg);
                        await GenerateClient(app, cfg, cnTkn);
                    }
                });

        /// <summary>
        /// exports an openapi json file to disk for a given document if the application is run with the commandline argument '<c>--exportopenapijson true</c>'
        /// and exits the
        /// program with a zero exit code.
        /// <para>HINT: make sure to place the call straight after '<c>app.UseFastEndpoints()</c>'</para>
        /// </summary>
        /// <param name="documentName">the name of the openapi document to generate the clients for</param>
        /// <param name="destinationPath">the folder path (without file name) where the client files will be saved to</param>
        /// <param name="destinationFileName">optional output file name with extension. defaults to <c>{documentName}.json</c></param>
        public Task ExportOpenApiJsonAndExitAsync(string documentName, string destinationPath, string? destinationFileName = null, CancellationToken ct = default)
            => app.RunGenerationModeAsync(
                app.IsOpenApiJsonExportMode(),
                ct,
                cnTkn => ExportOpenApiJson(app, documentName, destinationPath, destinationFileName, cnTkn));

        /// <summary>
        /// exports multiple openapi json files to disk if the application is run with the commandline argument '<c>--exportopenapijson true</c>' and exits the
        /// program with a zero
        /// exit code.
        /// <para>HINT: make sure to place the call straight after '<c>app.UseFastEndpoints()</c>'</para>
        /// </summary>
        /// <param name="ct">cancellation token</param>
        /// <param name="configs">openapi doc export configurations</param>
        public Task ExportOpenApiJsonAndExitAsync(CancellationToken ct, params Action<OpenApiJsonExportConfig>[] configs)
            => app.RunGenerationModeAsync(
                app.IsOpenApiJsonExportMode(),
                ct,
                async cnTkn =>
                {
                    foreach (var config in configs)
                    {
                        var cfg = new OpenApiJsonExportConfig();
                        config(cfg);
                        ValidateOpenApiJsonExportConfig(cfg);
                        await ExportOpenApiJson(app, cfg.DocumentName, cfg.DestinationPath, cfg.DestinationFileName, cnTkn);
                    }
                });

        /// <summary>
        /// returns true if the app is being launched just to generate api clients.
        /// </summary>
        public bool IsApiClientGenerationMode()
            => app.HasGenerationModeEnabled(ApiClientGenerationKey);

        /// <summary>
        /// returns true if the app is being launched just to export openapi json files.
        /// </summary>
        public bool IsOpenApiJsonExportMode()
            => app.HasGenerationModeEnabled(OpenApiJsonExportKey);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of generating api clients and/or exporting openapi json files.
        /// </summary>
        public bool IsNotGenerationMode()
            => !app.IsApiClientGenerationMode() && !app.IsOpenApiJsonExportMode();

        Task RunGenerationModeAsync(bool shouldRun, CancellationToken ct, Func<CancellationToken, Task> action)
            => !shouldRun ? Task.CompletedTask : ExecuteGenerationModeAsync(app, action, ct);

        bool HasGenerationModeEnabled(string key)
            => string.Equals(app.Services.GetRequiredService<IConfiguration>()[key], "true", StringComparison.Ordinal);
    }

    extension(IHostApplicationBuilder bld)
    {
        /// <summary>
        /// returns true if the app is being launched just to generate api clients.
        /// </summary>
        public bool IsApiClientGenerationMode()
            => bld.HasGenerationModeEnabled(ApiClientGenerationKey);

        /// <summary>
        /// returns true if the app is being launched just to export openapi json files.
        /// </summary>
        public bool IsOpenApiJsonExportMode()
            => bld.HasGenerationModeEnabled(OpenApiJsonExportKey);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of generating api clients and/or exporting openapi json files.
        /// </summary>
        public bool IsNotGenerationMode()
            => !bld.IsApiClientGenerationMode() && !bld.IsOpenApiJsonExportMode();

        bool HasGenerationModeEnabled(string key)
            => string.Equals(bld.Configuration[key], "true", StringComparison.Ordinal);
    }

    static async Task ExecuteGenerationModeAsync(IHost app, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        await app.StartAsync(ct);

        try
        {
            await action(ct);
        }
        finally
        {
            await app.StopAsync(ct);
        }

        Environment.Exit(0);
    }

    static async Task<string> ExportOpenApiJson(IHost app, string documentName, string destinationPath, string? destinationFileName, CancellationToken ct)
    {
        ValidateOpenApiJsonExportConfig(documentName, destinationPath);
        var documentKey = documentName.ToLowerInvariant();

        var logger = app.Services.GetRequiredService<ILogger<ClientGenerator>>();
        logger.ExportingOpenApiSpec(documentName);

        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentKey);
        var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentKey).OpenApiVersion;
        var doc = await provider.GetOpenApiDocumentAsync(ct);
        var json = await doc.SerializeAsJsonAsync(openApiVersion, ct);

        var file = NormalizeOutputFileName(documentName, destinationFileName);
        var output = Path.Combine(destinationPath, file);
        Directory.CreateDirectory(destinationPath);
        await File.WriteAllTextAsync(output, json, ct);

        logger.OpenApiSpecExportSuccessful();

        return output;
    }

    static async Task GenerateClient(IHost app, ClientGenConfig c, CancellationToken ct)
    {
        ValidateClientGenerationConfig(c);

        var logger = app.Services.GetRequiredService<ILogger<ClientGenerator>>();
        logger.StartingApiClientGeneration(c.Language.ToString(), c.OpenApiDocumentName);

        using var workspace = GenerationWorkspace.Create(c.OutputPath, c.CleanOutput);

        c.OpenAPIFilePath = await ExportOpenApiJson(app, c.OpenApiDocumentName, workspace.OpenApiDocumentPath, null, ct);

        await new KiotaBuilder(
                logger: LoggerFactory.Create(_ => { }).CreateLogger<KiotaBuilder>(),
                config: c,
                client: new())
            .GenerateClientAsync(ct);

        logger.ApiClientGenerationSuccessful();

        if (!c.CreateZipArchive)
            return;

        logger.ZippingGeneratedClientFile();

        c.ZipOutputFile ??= Path.Combine(c.OutputPath, $"..{Path.DirectorySeparatorChar}", $"{c.ClientClassName}.zip");

        if (File.Exists(c.ZipOutputFile))
            File.Delete(c.ZipOutputFile);

        ZipFile.CreateFromDirectory(c.OutputPath, c.ZipOutputFile, CompressionLevel.SmallestSize, false, Encoding.UTF8);

        logger.ClientArchiveCreationSuccessful();
    }

    static string NormalizeOutputFileName(string documentName, string? destinationFileName)
        => (destinationFileName ?? documentName + ".json").ToLowerInvariant().Replace(" ", "-");

    static void ValidateClientGenerationConfig(ClientGenConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(config.OpenApiDocumentName);
        ArgumentException.ThrowIfNullOrEmpty(config.OutputPath);
        ArgumentException.ThrowIfNullOrEmpty(config.ClientClassName);
    }

    static void ValidateOpenApiJsonExportConfig(OpenApiJsonExportConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateOpenApiJsonExportConfig(config.DocumentName, config.DestinationPath);
    }

    static void ValidateOpenApiJsonExportConfig(string documentName, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentName);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);
    }

    static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort cleanup after the response is already being sent
        }
    }

    static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // best effort cleanup after the response is already being sent
        }
    }

    sealed class GenerationWorkspace : IDisposable
    {
        readonly string? _temporaryRoot;

        GenerationWorkspace(string outputPath, string? temporaryRoot)
        {
            OutputPath = outputPath;
            _temporaryRoot = temporaryRoot;
            OpenApiDocumentPath = temporaryRoot ?? outputPath;
        }

        public string OutputPath { get; }
        public string OpenApiDocumentPath { get; }

        public static GenerationWorkspace Create(string outputPath, bool cleanOutput)
        {
            if (!cleanOutput)
                return new(outputPath, null);

            var root = CreateRootPath();

            return new(outputPath, root);
        }

        public static string CreateRootPath()
        {
            var path = Path.Combine(Path.GetTempPath(), TempFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);

            return path;
        }

        public void Dispose()
        {
            if (_temporaryRoot is null)
                return;

            try
            {
                if (Directory.Exists(_temporaryRoot))
                    Directory.Delete(_temporaryRoot, true);
            }
            catch
            {
                // best effort cleanup for temp generation artifacts
            }
        }
    }
}

// ReSharper disable once RedundantTypeDeclarationBody
public class ClientGenerator { }