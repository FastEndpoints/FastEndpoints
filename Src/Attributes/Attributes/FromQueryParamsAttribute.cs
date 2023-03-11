namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will be bound by obtaining the values from query string parameters with matching names.
/// <para>WARNING:
/// valid only on complex types with at least one public property.
/// only one dto property can be decorated with this attribute.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class FromQueryParamsAttribute : QueryParamAttribute { }