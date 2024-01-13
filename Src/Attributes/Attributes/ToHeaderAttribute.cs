namespace FastEndpoints;

/// <summary>
/// response dto properties marked with this attribute will cause an automatic response header to be added to the http response with the value from the property  that is
/// annotated.
/// </summary>
/// <param name="headerName">a custom name for the header. if not supplied, the property name will be used.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ToHeaderAttribute(string? headerName = null) : Attribute
{
    /// <summary>
    /// a custom name for the header. if not supplied, the property name will be used.
    /// </summary>
    public string? HeaderName { get; set; } = headerName;
}