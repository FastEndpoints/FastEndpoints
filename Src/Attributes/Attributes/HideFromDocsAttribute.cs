namespace FastEndpoints;

/// <summary>
/// attribute used to mark classes that should be hidden from public api
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public class HideFromDocsAttribute : Attribute { }

/// <summary>
/// endpoint properties marked with this attribute will disable property injection for that property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DontInjectAttribute : Attribute { }
