namespace FastEndpoints;

/// <summary></summary>
public abstract class NonJsonBindingAttribute : Attribute;

/// <summary>
/// you can prevent one or more binding sources from supplying values for a dto property decorated with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DontBindAttribute : NonJsonBindingAttribute
{
    /// <summary>
    /// gets the disabled binding sources.
    /// </summary>
    public Source BindingSources { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the request doesn't have a value for this property.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// specify a bitwise combination of binding sources to disable for the property.
    /// </summary>
    public DontBindAttribute(Source sources)
    {
        BindingSources = sources;
    }
}

/// <summary>
/// disables all other binding sources for a dto property except form fields.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormFieldAttribute() : DontBindAttribute(Source.QueryParam | Source.RouteParam);

/// <summary>
/// disables all other binding sources for a dto property except route params.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RouteParamAttribute() : DontBindAttribute(Source.QueryParam | Source.FormField);

/// <summary>
/// disables all other binding sources for a dto property except query params.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class QueryParamAttribute() : DontBindAttribute(Source.RouteParam | Source.FormField);