using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiExporter
{
    public static async Task ExportDocsAndExitAsync(WebApplication app, string[] documentNames)
    {
        if (app.IsNotJsonExportMode())
            return;

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
                try
                {
                    logger.ExportingOpenApiDoc(docName);
                    var filePath = await ExportDocumentAsync(app, docName, destinationPath, CancellationToken.None);
                    logger.OpenApiDocExportSuccessful(docName, filePath);
                }
                catch (Exception ex)
                {
                    exportFailed = true;
                    logger.OpenApiDocExportFailed(ex, docName);
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }

        Environment.Exit(exportFailed ? 1 : 0);
    }

    static async Task<string> ExportDocumentAsync(WebApplication app, string documentName, string destinationPath, CancellationToken ct)
    {
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentName);
        var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName).OpenApiVersion;
        var doc = await provider.GetOpenApiDocumentAsync(ct);
        var json = await doc.SerializeAsJsonAsync(openApiVersion, ct);
        var filePath = Path.Combine(destinationPath, $"{documentName}.json");

        await File.WriteAllTextAsync(filePath, json, ct);

        return filePath;
    }
}
