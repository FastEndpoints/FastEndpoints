namespace FastEndpoints;

/// <summary>
/// attribute used to mark classes that should be hidden from public api
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
public sealed class HideFromDocsAttribute : Attribute { }
