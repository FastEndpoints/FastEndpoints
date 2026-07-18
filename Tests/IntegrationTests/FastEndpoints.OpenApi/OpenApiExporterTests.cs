using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace OpenApi;

public class OpenApiExporterTests
{
    [Fact]
    public async Task neither_flag_is_noop_returns_null()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: false, exportHttp: false);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, ["v1"]);

        code.ShouldBeNull();
        Directory.Exists(ctx.ExportDir).ShouldBeFalse();
    }

    [Fact]
    public async Task empty_document_names_returns_zero_without_hang()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: true, exportHttp: true);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, []);

        code.ShouldBe(0);
        Directory.Exists(ctx.ExportDir).ShouldBeFalse();
    }

    [Fact]
    public async Task json_only_writes_json_file()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: true, exportHttp: false);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, ["v1"]);

        code.ShouldBe(0);
        File.Exists(Path.Combine(ctx.ExportDir, "v1.json")).ShouldBeTrue();
        File.Exists(Path.Combine(ctx.ExportDir, "v1.http")).ShouldBeFalse();
    }

    [Fact]
    public async Task http_only_writes_http_file()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: false, exportHttp: true);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, ["v1"]);

        code.ShouldBe(0);
        File.Exists(Path.Combine(ctx.ExportDir, "v1.json")).ShouldBeFalse();
        File.Exists(Path.Combine(ctx.ExportDir, "v1.http")).ShouldBeTrue();
    }

    [Fact]
    public async Task both_flags_one_invocation_writes_both_formats()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: true, exportHttp: true);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, ["v1"]);

        code.ShouldBe(0);
        File.Exists(Path.Combine(ctx.ExportDir, "v1.json")).ShouldBeTrue();
        File.Exists(Path.Combine(ctx.ExportDir, "v1.http")).ShouldBeTrue();
    }

    [Fact]
    public async Task unknown_document_name_returns_nonzero()
    {
        await using var ctx = await ExportTestContext.CreateAsync(exportJson: true, exportHttp: true);

        var code = await OpenApiExporter.ExportRequestedFormatsAsync(ctx.App, ["missing"]);

        code.ShouldBe(1);
        File.Exists(Path.Combine(ctx.ExportDir, "missing.json")).ShouldBeFalse();
        File.Exists(Path.Combine(ctx.ExportDir, "missing.http")).ShouldBeFalse();
    }

    sealed class ExportTestContext : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required string ContentRoot { get; init; }
        public required string ExportDir { get; init; }
        public required string PreviousExportPath { get; init; }

        public static Task<ExportTestContext> CreateAsync(bool exportJson, bool exportHttp)
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "fe-openapi-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            const string exportRel = "export-out";
            var previousPath = DocumentOptions.OpenApiExportPath;
            DocumentOptions.OpenApiExportPath = exportRel;

            var config = new Dictionary<string, string?>();

            if (exportJson)
                config["export-openapi-docs"] = "true";

            if (exportHttp)
                config["export-http-files"] = "true";

            var bld = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot });
            bld.Configuration.AddInMemoryCollection(config);

            // isolated fake provider — avoid harness endpoint discovery / global Config pollution
            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "export-test", Version = "1.0.0" },
                Paths = new OpenApiPaths
                {
                    ["/ping"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<HttpMethod, OpenApiOperation>
                        {
                            [HttpMethod.Get] = new() { OperationId = "Ping" }
                        }
                    }
                }
            };

            bld.Services.AddKeyedSingleton<IOpenApiDocumentProvider>("v1", new FakeOpenApiDocumentProvider(document));
            bld.Services.AddSingleton<IOptionsMonitor<OpenApiOptions>>(new StaticOpenApiOptionsMonitor(new OpenApiOptions()));

            var app = bld.Build();

            return Task.FromResult(
                new ExportTestContext
                {
                    App = app,
                    ContentRoot = contentRoot,
                    ExportDir = Path.Combine(contentRoot, exportRel),
                    PreviousExportPath = previousPath
                });
        }

        public async ValueTask DisposeAsync()
        {
            DocumentOptions.OpenApiExportPath = PreviousExportPath;

            await App.DisposeAsync();

            try
            {
                if (Directory.Exists(ContentRoot))
                    Directory.Delete(ContentRoot, recursive: true);
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }

    sealed class FakeOpenApiDocumentProvider(OpenApiDocument document) : IOpenApiDocumentProvider
    {
        public Task<OpenApiDocument> GetOpenApiDocumentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(document);
    }

    sealed class StaticOpenApiOptionsMonitor(OpenApiOptions options) : IOptionsMonitor<OpenApiOptions>
    {
        public OpenApiOptions CurrentValue => options;
        public OpenApiOptions Get(string? name) => options;
        public IDisposable? OnChange(Action<OpenApiOptions, string?> listener) => null;
    }
}
