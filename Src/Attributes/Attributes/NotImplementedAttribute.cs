namespace FastEndpoints;

/// <summary>
/// indicates a base/abstract method that's not implemented.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NotImplementedAttribute : Attribute;