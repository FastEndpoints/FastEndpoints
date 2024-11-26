using System.IO.Compression;
using System.Text;
using Kiota.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSwag.Generation;

namespace FastEndpoints.ClientGen.Kiota;

#pragma warning disable VSTHRD200

public static class Extensions
{
    const string TempFolder = "KiotaClientGen";

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
                var c = new ClientGenConfig { OutputPath = Path.Combine(Path.GetTempPath(), TempFolder) };
                config(c);
                c.CreateZipArchive = true;

                await GenerateClient(app, c, ct);

                var zipFile = Path.GetFullPath(c.ZipOutputFile!);
                await Results.File(
                                 path: zipFile,
                                 contentType: "application/octet-stream",
                                 enableRangeProcessing: true,
                                 fileDownloadName: Path.GetFileName(zipFile))
                             .ExecuteAsync(httpCtx);

                File.Delete(zipFile);
            });

        options?.Invoke(b);

        return builder;
    }

    /// <summary>
    /// generates api clients based on supplied configurations and saves them to disk if the application is run with the commandline argument
    /// '<c>--generateclients true</c>' and exits the program with a zero exit code.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// </summary>
    /// <param name="configs">client generation configurations</param>
    public static Task GenerateApiClientsAndExitAsync(this IHost app, params Action<ClientGenConfig>[] configs)
        => GenerateApiClientsAndExitAsync(app, default, configs);

    /// <summary>
    /// generates api clients based on supplied configurations and saves them to disk if the application is run with the commandline argument
    /// '<c>--generateclients true</c>' and exits the program with a zero exit code.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// </summary>
    /// <param name="ct">cancellation token</param>
    /// <param name="configs">client generation configurations</param>
    public static async Task GenerateApiClientsAndExitAsync(this IHost app, CancellationToken ct, params Action<ClientGenConfig>[] configs)
    {
        if (app.IsApiClientGenerationMode())
        {
            await app.StartAsync(ct);

            foreach (var config in configs)
            {
                var c = new ClientGenConfig();
                config(c);
                await GenerateClient(app, c, ct);
            }

            await app.StopAsync(ct);
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// exports a swagger.json file to disk for a given swagger document if the application is run with the commandline argument '<c>--exportswaggerjson true</c>'
    /// and exits the
    /// program with a zero exit code.
    /// <para>HINT: make sure to place the call straight after '<c>app.UseFastEndpoints()</c>'</para>
    /// </summary>
    /// <param name="documentName">the name of the swagger document to generate the clients for</param>
    /// <param name="destinationPath">the folder path (without file name) where the client files will be saved to</param>
    /// <param name="destinationFileName">optional output file name with extension. defaults to <c>{documentName}.json</c></param>
    public static async Task ExportSwaggerJsonAndExitAsync(this IHost app,
                                                           string documentName,
                                                           string destinationPath,
                                                           string? destinationFileName = null,
                                                           CancellationToken ct = default)
    {
        if (app.IsSwaggerJsonExportMode())
        {
            await app.StartAsync(ct);
            await ExportSwaggerJson(app, documentName, destinationPath, destinationFileName, ct);
            await app.StopAsync(ct);
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// exports multiple swagger.json files to disk if the application is run with the commandline argument '<c>--exportswaggerjson true</c>' and exits the
    /// program with a zero
    /// exit code.
    /// <para>HINT: make sure to place the call straight after '<c>app.UseFastEndpoints()</c>'</para>
    /// </summary>
    /// <param name="ct">cancellation token</param>
    /// <param name="configs">swagger doc export configurations</param>
    public static async Task ExportSwaggerJsonAndExitAsync(this IHost app, CancellationToken ct, params Action<SwaggerJsonExportConfig>[] configs)
    {
        if (app.IsSwaggerJsonExportMode())
        {
            await app.StartAsync(ct);

            foreach (var c in configs)
            {
                var cfg = new SwaggerJsonExportConfig();
                c(cfg);
                ArgumentException.ThrowIfNullOrEmpty(cfg.DocumentName);
                ArgumentException.ThrowIfNullOrEmpty(cfg.DestinationPath);
                await ExportSwaggerJson(app, cfg.DocumentName, cfg.DestinationPath, cfg.DestinationFileName, ct);
            }
            await app.StopAsync(ct);
            Environment.Exit(0);
        }
    }

    const string ApiClientGenerationKey = "generateclients";

    /// <summary>
    /// returns true if the app is being launched just to generate api clients.
    /// </summary>
    public static bool IsApiClientGenerationMode(this IHostApplicationBuilder bld)
        => bld.Configuration[ApiClientGenerationKey] == "true";

    /// <summary>
    /// returns true if the app is being launched just to generate api clients.
    /// </summary>
    public static bool IsApiClientGenerationMode(this IHost app)
        => app.Services.GetRequiredService<IConfiguration>()[ApiClientGenerationKey] == "true";

    const string SwaggerJsonExportKey = "exportswaggerjson";

    /// <summary>
    /// returns true if the app is being launched just to export swagger json files.
    /// </summary>
    public static bool IsSwaggerJsonExportMode(this IHostApplicationBuilder bld)
        => bld.Configuration[SwaggerJsonExportKey] == "true";

    /// <summary>
    /// returns true if the app is being launched just to export swagger json files.
    /// </summary>
    public static bool IsSwaggerJsonExportMode(this IHost app)
        => app.Services.GetRequiredService<IConfiguration>()[SwaggerJsonExportKey] == "true";

    /// <summary>
    /// returns true if the app is running normally and not launched for the purpose of generating api clients and/or exporting swagger json files.
    /// </summary>
    public static bool IsNotGenerationMode(this IHostApplicationBuilder bld)
        => !bld.IsApiClientGenerationMode() && !bld.IsSwaggerJsonExportMode();

    /// <summary>
    /// returns true if the app is running normally and not launched for the purpose of generating api clients and/or exporting swagger json files.
    /// </summary>
    public static bool IsNotGenerationMode(this IHost app)
        => !app.IsApiClientGenerationMode() && !app.IsSwaggerJsonExportMode();

    static async Task<string> ExportSwaggerJson(IHost app, string documentName, string destinationPath, string? destinationFileName, CancellationToken ct)
    {
        var logger = app.Services.GetRequiredService<ILogger<ClientGenerator>>();
        logger.LogInformation("Exporting Swagger Spec for doc: [{doc}]", documentName);

        var generator = app.Services.GetRequiredService<IOpenApiDocumentGenerator>();
        var doc = await generator.GenerateAsync(documentName);
        var file = (destinationFileName ?? documentName + ".json").ToLowerInvariant().Replace(" ", "-");
        var output = Path.Combine(destinationPath, file);
        Directory.CreateDirectory(destinationPath);
        await File.WriteAllTextAsync(output, doc.ToJson(), ct);

        logger.LogInformation("Swagger Spec export successful!");

        return output;
    }

    static async Task GenerateClient(IHost app, ClientGenConfig c, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(c.SwaggerDocumentName))
            throw new InvalidOperationException("A Swagger document name is required for Api Client generation!");

        var logger = app.Services.GetRequiredService<ILogger<ClientGenerator>>();
        logger.LogInformation("Starting [{lang}] Api Client generation for [{doc}]", c.Language.ToString(), c.SwaggerDocumentName);

        var swaggerJsonPath = c.CleanOutput
                                  ? Path.Combine(Path.GetTempPath(), TempFolder)
                                  : c.OutputPath;

        c.OpenAPIFilePath = await ExportSwaggerJson(app, c.SwaggerDocumentName, swaggerJsonPath, null, ct);

        await new KiotaBuilder(
                logger: LoggerFactory.Create(_ => { }).CreateLogger<KiotaBuilder>(),
                config: c,
                client: new())
            .GenerateClientAsync(ct);

        logger.LogInformation("Api Client generation successful!");

        if (c.CreateZipArchive)
        {
            logger.LogInformation("Zipping up the generated client files...");

            c.ZipOutputFile ??= Path.Combine(c.OutputPath, $"..{Path.DirectorySeparatorChar}", $"{c.ClientClassName}.zip");

            if (File.Exists(c.ZipOutputFile))
                File.Delete(c.ZipOutputFile);

            ZipFile.CreateFromDirectory(c.OutputPath, c.ZipOutputFile, CompressionLevel.SmallestSize, false, Encoding.UTF8);

            logger.LogInformation("Client archive creation successful!");
        }
    }
}

// ReSharper disable once RedundantTypeDeclarationBody
public class ClientGenerator { }