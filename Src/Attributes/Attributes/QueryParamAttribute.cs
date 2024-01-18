namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have a corresponding request parameter added to the swagger schema
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class QueryParamAttribute : Attribute;