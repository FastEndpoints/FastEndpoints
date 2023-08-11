namespace FastEndpoints;

/// <summary>
/// classes marked with this attribute will be skipped during assembly scanning for auto registration
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DontRegisterAttribute : Attribute { }
