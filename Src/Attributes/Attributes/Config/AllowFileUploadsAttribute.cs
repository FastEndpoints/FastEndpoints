namespace FastEndpoints;

/// <summary>
/// enable file uploads with multipart/form-data content type
/// </summary>
/// <param name="dontAutoBindFormData">
/// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
/// you can access the multipart sections for reading via the <c>FormFileSectionsAsync()</c> method.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowFileUploadsAttribute(bool dontAutoBindFormData = false) : Attribute
{
    /// <summary>
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the <c>FormFileSectionsAsync()</c> method.
    /// </summary>
    public bool DontAutoBindFormData { get; set; } = dontAutoBindFormData;
}