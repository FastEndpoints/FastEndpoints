namespace FastEndpoints;

/// <summary>
/// endpoint properties marked with this attribute will disable property injection for that property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DontInjectAttribute : Attribute { }
