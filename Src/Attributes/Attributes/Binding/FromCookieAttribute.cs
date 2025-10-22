namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant cookie of the current request.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FromCookieAttribute : NonJsonBindingAttribute
{
    /// <summary>
    /// the cookie name to auto bind from
    /// </summary>
    public string? CookieName { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current request doesn't have the specified cookie
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// set to true if your cookie is not required but shouldn't be added to schema model
    /// </summary>
    public bool RemoveFromSchema { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http cookie of the current request.
    /// </summary>
    /// <param name="isRequired">
    /// set to false if a validation error shouldn't be thrown when the current user request doesn't have a cookie matching the property name being
    /// bound to.
    /// </param>
    /// <param name="removeFromSchema">set to true if your cookie is not required but shouldn't be added to schema model.</param>
    public FromCookieAttribute(bool isRequired, bool removeFromSchema = false)
    {
        CookieName = null;
        IsRequired = isRequired;
        RemoveFromSchema = removeFromSchema;
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http cookie of the current request.
    /// </summary>
    /// <param name="cookieName">optionally specify the cookie name to bind from. if not specified, the cookie name must match the name of the property being bound to.</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current request doesn't have the specified cookie.</param>
    /// <param name="removeFromSchema">set to true if your cookie is not required but shouldn't be added to schema model.</param>
    public FromCookieAttribute(string? cookieName = null, bool isRequired = true, bool removeFromSchema = false)
    {
        CookieName = cookieName;
        IsRequired = isRequired;
        RemoveFromSchema = removeFromSchema;
    }
}