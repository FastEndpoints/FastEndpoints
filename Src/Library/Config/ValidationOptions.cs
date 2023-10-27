namespace FastEndpoints;

/// <summary>
/// validation related options
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// set this property to <c>true</c> if you'd like to enable support for <c>System.ComponentModel.DataAnnotations</c> attributes for basic validation.
    /// </summary>
    public bool EnableDataAnnotationsSupport { internal get; set; }
}