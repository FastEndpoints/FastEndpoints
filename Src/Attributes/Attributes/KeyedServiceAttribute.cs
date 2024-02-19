namespace FastEndpoints;

/// <summary>
/// use this attribute to mark a property to be auto injected from the DI container.
/// </summary>
/// <param name="keyName">the key name</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyedServiceAttribute(string keyName) : Attribute
{
    /// <summary>
    /// the key to use for obtaining the service from the DI container.
    /// </summary>
    public string Key { get; } = keyName;
}