using Kiota.Builder.Configuration;

namespace FastEndpoints.ClientGen.Kiota;

/// <inheritdoc />
public sealed class ClientGenConfig : GenerationConfiguration
{
    /// <summary>
    /// the swagger document name for which the clients are to be generated
    /// </summary>
    public string SwaggerDocumentName { get; set; } = null!;

    /// <summary>
    /// specify whether to zip up the generated client files.
    /// <para>
    /// NOTE: this property has no effect when client generation endpoints are mapped as the creation of a zip archive is mandatory
    /// in order to provide a downloadable byte stream.
    /// </para>
    /// </summary>
    public bool CreateZipArchive { get; set; }

    /// <summary>
    /// specify the output path for the zip archive including full path and file extension.
    /// the default location is one level up from the folder that contains the generated client files.
    /// </summary>
    public string? ZipOutputFile { get; set; }
}