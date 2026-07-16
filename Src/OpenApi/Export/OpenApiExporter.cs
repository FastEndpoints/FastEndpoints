using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiExporter
{
    public static Task ExportDocsAndExitAsync(WebApplication app, string[] documentNames)
        => app.IsNotJsonExportMode() ? Task.CompletedTask : RunExportAsync(app, documentNames, ExportJsonDocumentAsync);

    public static Task ExportHttpFilesAndExitAsync(WebApplication app, string[] documentNames)
        => app.IsNotHttpExportMode() ? Task.CompletedTask : RunExportAsync(app, documentNames, ExportHttpDocumentAsync);

    static async Task RunExportAsync(WebApplication app,
                                      string[] documentNames,
                                      Func<WebApplication, string, string, ILogger, CancellationToken, Task<bool>> exportOneAsync)
    {
        if (documentNames.Length == 0)
            return;

        var destinationPath = Path.Combine(app.Environment.ContentRootPath, DocumentOptions.OpenApiExportPath);
        var logger = app.Services.GetRequiredService<ILogger<OpenApiExportRunner>>();

        await app.StartAsync();
        var exportFailed = false;

        try
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var docName in documentNames)
            {
                if (!await exportOneAsync(app, docName, destinationPath, logger, CancellationToken.None))
                    exportFailed = true;
            }
        }
        finally
        {
            await app.StopAsync();
        }

        Environment.Exit(exportFailed ? 1 : 0);
    }

    static async Task<bool> ExportJsonDocumentAsync(WebApplication app, string documentName, string destinationPath, ILogger logger, CancellationToken ct)
    {
        try
        {
            logger.ExportingOpenApiDoc(documentName);
            var normalizedDocumentName = documentName.ToLowerInvariant();
            var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
            var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(normalizedDocumentName).OpenApiVersion;
            var doc = await provider.GetOpenApiDocumentAsync(ct);
            var json = await doc.SerializeAsJsonAsync(openApiVersion, ct);
            var filePath = Path.Combine(destinationPath, $"{normalizedDocumentName}.json");

            await File.WriteAllTextAsync(filePath, json, ct);
            logger.OpenApiDocExportSuccessful(documentName, filePath);

            return true;
        }
        catch (Exception ex)
        {
            logger.OpenApiDocExportFailed(ex, documentName);

            return false;
        }
    }

    static async Task<bool> ExportHttpDocumentAsync(WebApplication app, string documentName, string destinationPath, ILogger logger, CancellationToken ct)
    {
        try
        {
            logger.ExportingHttpFile(documentName);
            var normalizedDocumentName = documentName.ToLowerInvariant();
            var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
            var doc = await provider.GetOpenApiDocumentAsync(ct);
            var http = HttpFileExporter.ToHttpFileContent(doc, normalizedDocumentName);
            var filePath = Path.Combine(destinationPath, $"{normalizedDocumentName}.http");

            await File.WriteAllTextAsync(filePath, http, ct);
            logger.HttpFileExportSuccessful(documentName, filePath);

            return true;
        }
        catch (Exception ex)
        {
            logger.HttpFileExportFailed(ex, documentName);

            return false;
        }
    }
}
