namespace FastEndpoints;

/// <summary>
/// if a request dto property is decorated with this attribute, that property will be bound from complex query parameter data from the  incoming request.
/// only valid on complex type properties. only one dto property can be decorated. the incoming query parameters should be in the correct format.
/// <para>
/// HINT: recursively binding complex object graphs from query parameters is less performant than binding to top level primitive dto properties.
/// so... use sparingly!
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FromQueryAttribute : QueryParamAttribute;