using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.OpenApi.Kiota;
using Kiota.Builder;

var artifactRoot = Environment.GetEnvironmentVariable("FE_KIOTA_ARTIFACT_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "artifacts");

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .AddFastEndpoints()
   .OpenApiDocument(
       o =>
       {
           o.DocumentSettings = s =>
                                {
                                    s.DocumentName = "Kiota Release";
                                    s.Title = "Kiota Harness";
                                    s.Version = "v1";
                                };
       });

var app = bld.Build();
app.UseFastEndpoints();
app.MapOpenApi();

await app.GenerateApiClientsAndExitAsync(
    c =>
    {
        c.OpenApiDocumentName = "Kiota Release";
        c.ClientClassName = "HarnessApiClient";
        c.Language = GenerationLanguage.CSharp;
        c.OutputPath = Path.Combine(artifactRoot, "generated-client");
        c.CleanOutput = true;
        c.CreateZipArchive = true;
    });

await app.ExportOpenApiJsonAndExitAsync(
    ct: CancellationToken.None,
    c =>
    {
        c.DocumentName = "Kiota Release";
        c.DestinationPath = Path.Combine(artifactRoot, "openapi-json");
        c.DestinationFileName = "Kiota Spec.JSON";
    });

app.MapApiClientEndpoint(
    "/api-client",
    c =>
    {
        c.OpenApiDocumentName = "Kiota Release";
        c.ClientClassName = "HarnessApiClient";
        c.Language = GenerationLanguage.CSharp;
    });

app.Run();

public partial class Program;