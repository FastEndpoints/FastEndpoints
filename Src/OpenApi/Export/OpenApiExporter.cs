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

    static Task<bool> ExportJsonDocumentAsync(WebApplication app, string documentName, string destinationPath, ILogger logger, CancellationToken ct)
    {
        logger.ExportingOpenApiDoc(documentName);
        var normalizedDocumentName = documentName.ToLowerInvariant();

        return WriteExportAsync(
            documentName, normalizedDocumentName, destinationPath, ".json", logger.OpenApiDocExportSuccessful, logger.OpenApiDocExportFailed, ct,
            async () =>
            {
                var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
                var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(normalizedDocumentName).OpenApiVersion;
                var doc = await provider.GetOpenApiDocumentAsync(ct);

                return await doc.SerializeAsJsonAsync(openApiVersion, ct);
            });
    }

    static Task<bool> ExportHttpDocumentAsync(WebApplication app, string documentName, string destinationPath, ILogger logger, CancellationToken ct)
    {
        logger.ExportingHttpFile(documentName);
        var normalizedDocumentName = documentName.ToLowerInvariant();

        return WriteExportAsync(
            documentName, normalizedDocumentName, destinationPath, ".http", logger.HttpFileExportSuccessful, logger.HttpFileExportFailed, ct,
            async () =>
            {
                var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
                var doc = await provider.GetOpenApiDocumentAsync(ct);

                return HttpFileExporter.ToHttpFileContent(doc, normalizedDocumentName);
            });
    }

    static async Task<bool> WriteExportAsync(string documentName,
                                              string normalizedDocumentName,
                                              string destinationPath,
                                              string extension,
                                              Action<string, string> logSuccess,
                                              Action<Exception, string> logFailure,
                                              CancellationToken ct,
                                              Func<Task<string>> produceContent)
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
