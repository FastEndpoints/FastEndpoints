using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
}