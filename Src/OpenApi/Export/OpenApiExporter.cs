using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiExporter
{
    /// <summary>
    /// exports every CLI-requested format and exits the process.
    /// no-ops when neither <c>--export-openapi-docs</c> nor <c>--export-http-files</c> is set.
    /// </summary>
    public static async Task ExportRequestedFormatsAndExitAsync(WebApplication app, string[] documentNames)
    {
        var exitCode = await ExportRequestedFormatsAsync(app, documentNames);

        if (exitCode is null)
            return;

        Environment.Exit(exitCode.Value);
    }

    /// <summary>
    /// core multi-format export orchestrator. returns null when not in any export mode;
    /// otherwise 0 on success / 1 on any failure (does not call <see cref="Environment.Exit" />).
    /// </summary>
    internal static async Task<int?> ExportRequestedFormatsAsync(WebApplication app, string[] documentNames)
    {
        var exportJson = app.IsJsonExportMode();
        var exportHttp = app.IsHttpExportMode();

        if (!exportJson && !exportHttp)
            return null;

        // empty name list still exits cleanly so dual-flag / single-call apps cannot hang
        if (documentNames.Length == 0)
            return 0;

        var destinationPath = Path.Combine(app.Environment.ContentRootPath, DocumentOptions.OpenApiExportPath);
        var logger = app.Services.GetRequiredService<ILogger<OpenApiExportRunner>>();
        var failed = false;

        await app.StartAsync();

        try
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var documentName in documentNames)
            {
                var normalizedDocumentName = documentName.ToLowerInvariant();
                OpenApiDocument doc;

                try
                {
                    var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
                    doc = await provider.GetOpenApiDocumentAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.OpenApiDocumentLoadFailed(ex, documentName);
                    failed = true;

                    continue;
                }

                if (exportJson)
                {
                    logger.ExportingOpenApiDoc(documentName);

                    if (!await WriteExportAsync(
                             documentName,
                             normalizedDocumentName,
                             destinationPath,
                             ".json",
                             logger.OpenApiDocExportSuccessful,
                             logger.OpenApiDocExportFailed,
                             async () =>
                             {
                                 var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(normalizedDocumentName).OpenApiVersion;

                                 return await doc.SerializeAsJsonAsync(openApiVersion, CancellationToken.None);
                             },
                             CancellationToken.None))
                        failed = true;
                }

                if (exportHttp)
                {
                    logger.ExportingHttpFile(documentName);

                    if (!await WriteExportAsync(
                             documentName,
                             normalizedDocumentName,
                             destinationPath,
                             ".http",
                             logger.HttpFileExportSuccessful,
                             logger.HttpFileExportFailed,
                             () => Task.FromResult(HttpFileExporter.ToHttpFileContent(doc)),
                             CancellationToken.None))
                        failed = true;
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }

        return failed ? 1 : 0;
    }

    static async Task<bool> WriteExportAsync(string documentName,
                                             string normalizedDocumentName,
                                             string destinationPath,
                                             string extension,
                                             Action<string, string> logSuccess,
                                             Action<Exception, string> logFailure,
                                             Func<Task<string>> produceContent,
                                             CancellationToken ct)
    {
        try
        {
            var content = await produceContent();
            var filePath = Path.Combine(destinationPath, $"{normalizedDocumentName}{extension}");

            await File.WriteAllTextAsync(filePath, content, ct);
            logSuccess(documentName, filePath);

            return true;
        }
        catch (Exception ex)
        {
            logFailure(ex, documentName);

            return false;
        }
    }
}