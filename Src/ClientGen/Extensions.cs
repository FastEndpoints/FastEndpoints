using NSwag.CodeGeneration;
using NSwag.CodeGeneration.CSharp;
using NSwag.Generation.AspNetCore;

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
}