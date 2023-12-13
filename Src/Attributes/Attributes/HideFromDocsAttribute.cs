namespace FastEndpoints;

/// <summary>
/// attribute used to mark classes, properties, methods that should be hidden from public api
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class HideFromDocsAttribute : Attribute { }