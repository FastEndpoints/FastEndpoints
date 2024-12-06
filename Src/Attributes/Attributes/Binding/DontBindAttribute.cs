namespace FastEndpoints;

/// <summary>
/// you can prevent one or more binding sources from supplying values for a dto property decorated with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DontBindAttribute : Attribute
{
    /// <summary>
    /// gets the disabled binding sources.
    /// </summary>
    public Source BindingSources { get; set; }

    /// <summary>
    /// specify a bitwise combination of binding sources to disable for the property.
    /// </summary>
    public DontBindAttribute(Source sources)
    {
        BindingSources = sources;
    }
}