namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromHeaderAttribute : Attribute
{
    /// <summary>
    /// the header name to auto bind from
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current request doesn't have the specified header
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
    /// </summary>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user request doesn't have a header matching the property name being bound to.</param>
    public FromHeaderAttribute(bool isRequired)
    {
        HeaderName = null;
        IsRequired = isRequired;
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
    /// </summary>
    /// <param name="headerName">optionally specify the header name to bind from. if not specified, the header name must match the name of the property being bound to.</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current request doesn't have the specified header.</param>
    public FromHeaderAttribute(string? headerName = null, bool isRequired = true)
    {
        HeaderName = headerName;
        IsRequired = isRequired;
    }
}
