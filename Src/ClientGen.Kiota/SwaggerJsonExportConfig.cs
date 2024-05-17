namespace FastEndpoints.ClientGen.Kiota;

/// <summary>
/// settings for exporting a swagger document as json
/// </summary>
public sealed class SwaggerJsonExportConfig
{
    /// <summary>
    /// the name of the swagger document to export
    /// </summary>
    public string DocumentName { get; set; } = null!;

    /// <summary>
    /// the folder path (without file name) where the client files will be saved to
    /// </summary>
    public string DestinationPath { get; set; } = null!;

    /// <summary>
    /// optional output file name with extension. defaults to <c>{documentName}.json</c>
    /// </summary>
    public string? DestinationFileName { get; set; }
}