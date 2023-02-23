using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSwag.CodeGeneration;
using NSwag.CodeGeneration.CSharp;
using NSwag.CodeGeneration.TypeScript;
using NSwag.Generation;
using NSwag.Generation.AspNetCore;
using System.Text;

namespace FastEndpoints.ClientGen;

public static class Extensions
{
    /// <summary>
    /// generates a c# api client and saves it to disk at the specified location.
    /// </summary>
    /// <param name="settings">client generator settings</param>
    /// <param name="destination">the output file path including file name</param>
    /// <param name="outputType">the type of the generated client file</param>
    public static void GenerateCSharpClient(this AspNetCoreOpenApiDocumentGeneratorSettings gen, Action<CSharpClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType = ClientGeneratorOutputType.Full)
        => gen.DocumentProcessors.Add(new CSClientGenProcessor(settings, destination, outputType));

    /// <summary>
    /// registers an endpoint that provides a download of the c# api client file for a given swagger document.
    /// </summary>
    /// <param name="route">the route to register</param>
    /// <param name="documentName">the name of the document to generate the client for</param>
    /// <param name="settings">c# client generator settings</param>
    public static IEndpointRouteBuilder MapCSharpClientEndpoint(this IEndpointRouteBuilder builder, string route, string documentName, Action<CSharpClientGeneratorSettings>? settings = null)
    {
        builder.MapGet(route, async (IOpenApiDocumentGenerator docs) =>
        {
            var generatorSettings = new CSharpClientGeneratorSettings()
            {
                ClassName = "ApiClient",
                CSharpGeneratorSettings = { Namespace = "FastEndpoints" }
            };
            settings?.Invoke(generatorSettings);

            var doc = await docs.GenerateAsync(documentName);
            var source = new CSharpClientGenerator(doc, generatorSettings).GenerateFile();

            return Results.File(
                fileContents: Encoding.UTF8.GetBytes(source),
                contentType: "application/octet-stream",
                fileDownloadName: generatorSettings.ClassName + ".cs");
        })
        .ExcludeFromDescription();
        return builder;
    }

    /// <summary>
    /// generates a typescript api client and saves it to disk at the specified location.
    /// </summary>
    /// <param name="settings">client generator settings</param>
    /// <param name="destination">the output file path including file name</param>
    /// <param name="outputType">the type of the generated client file</param>
    public static void GenerateTypeScriptClient(this AspNetCoreOpenApiDocumentGeneratorSettings gen, Action<TypeScriptClientGeneratorSettings> settings, string destination, ClientGeneratorOutputType outputType = ClientGeneratorOutputType.Full)
        => gen.DocumentProcessors.Add(new TSClientGenProcessor(settings, destination, outputType));

    /// <summary>
    /// registers an endpoint that provides a download of the typescript api client file for a given swagger document.
    /// </summary>
    /// <param name="route">the route to register</param>
    /// <param name="documentName">the name of the document to generate the client for</param>
    /// <param name="settings">typescript client generator settings</param>
    public static IEndpointRouteBuilder MapTypeScriptClientEndpoint(this IEndpointRouteBuilder builder, string route, string documentName, Action<TypeScriptClientGeneratorSettings>? settings = null)
    {
        builder.MapGet(route, async (IOpenApiDocumentGenerator docs) =>
        {
            var generatorSettings = new TypeScriptClientGeneratorSettings()
            {
                ClassName = "ApiClient",
                TypeScriptGeneratorSettings = { Namespace = "FastEndpoints" }
            };
            settings?.Invoke(generatorSettings);

            var doc = await docs.GenerateAsync(documentName);
            var source = new TypeScriptClientGenerator(doc, generatorSettings).GenerateFile();

            return Results.File(
                fileContents: Encoding.UTF8.GetBytes(source),
                contentType: "application/octet-stream",
                fileDownloadName: generatorSettings.ClassName + ".ts");
        })
        .ExcludeFromDescription();
        return builder;
    }

    /// <summary>
    /// generates c# and/or typescript clients and saves them to disk if the application is run with the commandline argument <c>--generateclients true</c>
    /// and exits the program with a zero exit code.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// </summary>
    /// <param name="documentName">the name of the swagger document to generate the clients for</param>
    /// <param name="destinationPath">the folder path (without file name) where the client files will be save to</param>
    /// <param name="csSettings">client generator settings for c#</param>
    /// <param name="tsSettings">client generator settings for typescript</param>
    public static async Task GenerateClientsAndExitAsync(this WebApplication app, string documentName, string destinationPath, Action<CSharpClientGeneratorSettings>? csSettings, Action<TypeScriptClientGeneratorSettings>? tsSettings)
    {
        if (app.Configuration["generateclients"] == "true")
        {
            if (tsSettings is null && csSettings is null)
                throw new InvalidOperationException("Either csharp or typescript client generator settings must be provided!");

            await app.StartAsync();
            var docs = app.Services.GetRequiredService<IOpenApiDocumentGenerator>();

            var logger = app.Services.GetRequiredService<ILogger<Runner>>();
            logger.LogInformation("Api client generation starting...");

            var doc = await docs.GenerateAsync(documentName);

            if (csSettings is not null)
            {
                var csGenSettings = new CSharpClientGeneratorSettings()
                {
                    ClassName = "ApiClient",
                    CSharpGeneratorSettings = { Namespace = "FastEndpoints" }
                };
                csSettings(csGenSettings);
                var source = new CSharpClientGenerator(doc, csGenSettings).GenerateFile();
                await File.WriteAllTextAsync(Path.Combine(destinationPath, csGenSettings.ClassName + ".cs"), source);
                logger.LogInformation("C# api client generation successful!");
            }

            if (tsSettings is not null)
            {
                var tsGenSettings = new TypeScriptClientGeneratorSettings()
                {
                    ClassName = "ApiClient",
                    TypeScriptGeneratorSettings = { Namespace = "FastEndpoints" }
                };
                tsSettings(tsGenSettings);
                var source = new TypeScriptClientGenerator(doc, tsGenSettings).GenerateFile();
                await File.WriteAllTextAsync(Path.Combine(destinationPath, tsGenSettings.ClassName + ".ts"), source);
                logger.LogInformation("TypeScript api client generation successful!");
            }

            await app.StopAsync();
            Environment.Exit(0);
        }
    }

    /// <summary>
    /// exports swagger.json files to disk if the application is run with the commandline argument <c>--exportswaggerjson true</c>
    /// and exits the program with a zero exit code.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// </summary>
    /// <param name="documentName">the name of the swagger document to generate the clients for</param>
    /// <param name="destinationPath">the folder path (without file name) where the client files will be save to</param>
    public static async Task ExportSwaggerJsonAndExitAsync(this WebApplication app, string documentName, string destinationPath)
    {
        if (app.Configuration["exportswaggerjson"] == "true")
        {
            await app.StartAsync();

            var logger = app.Services.GetRequiredService<ILogger<Runner>>();
            logger.LogInformation("Exporting json file for doc: [{doc}]", documentName);

            var generator = app.Services.GetRequiredService<IOpenApiDocumentGenerator>();
            var doc = await generator.GenerateAsync(documentName);
            var json = doc.ToJson();
            await File.WriteAllTextAsync(Path.Combine(destinationPath, documentName.ToLowerInvariant().Replace(" ", "-") + ".json"), json);
            logger.LogInformation("Swagger json export successful!");

            await app.StopAsync();
            Environment.Exit(0);
        }
    }
}

public class Runner { }