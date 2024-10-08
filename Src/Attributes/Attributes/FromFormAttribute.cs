namespace FastEndpoints;

/// <summary>
/// if a request dto property is decorated with this attribute, that property will be bound from complex multipart form data (including files) from the
/// incoming request. only valid on complex type properties. only one dto property can be decorated. the incoming form data should be in the correct format.
/// incoming content-type must be <c>multipart/form-data</c>
/// <para>
/// HINT: recursively binding complex object graphs from form data is less performant than binding to top level dto properties.
/// so... use sparingly!
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FromFormAttribute : Attribute;