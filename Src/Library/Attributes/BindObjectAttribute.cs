namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will be bound by constructing an object by getting values from query string parameters.
/// <para>WARNING:
/// valid only on complex types with at least one public property.
/// only one dto property can be decorated with this attribute.
/// an exception will be thrown if more than one dto property is annotated.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class BindObjectAttribute : QueryParamAttribute { }