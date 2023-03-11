namespace FastEndpoints;

/// <summary>
/// use this attribute to specify the name of route param, query param, or form field if it's different from the name of the property being bound to.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class BindFromAttribute : Attribute
{
    /// <summary>
    /// the name of the incoming query param, route param or form field
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// use this attribute to specify the name of route param, query param, or form field if it's different from the name of the property being bound to.
    /// </summary>
    /// <param name="name">the name to use for binding</param>
    public BindFromAttribute(string name)
    {
        Name = name;
    }
}
