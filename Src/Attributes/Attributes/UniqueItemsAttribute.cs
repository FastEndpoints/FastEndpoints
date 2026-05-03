namespace FastEndpoints;

/// <summary>
/// marks a collection property schema as requiring unique items in OpenAPI.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UniqueItemsAttribute : Attribute;
