namespace FastEndpoints;

/// <summary>
/// classes marked with this attribute will be skipped during assembly scanning for auto registration
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DontRegisterAttribute : Attribute;