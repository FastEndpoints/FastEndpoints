namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromClaimAttribute : Attribute
{
    /// <summary>
    /// the claim type to auto bind
    /// </summary>
    public string? ClaimType { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current user principal doesn't have the specified claim
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// set to true if your header is not required but shouldn't be added to schema model
    /// </summary>
    public bool RemoveFromSchema { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user principal doesn't have a claim type matching the property name being bound to.</param>
    /// <param name="removeFromSchema">set to true if your header is not required but shouldn't be added to schema model.</param>
    public FromClaimAttribute(bool isRequired, bool removeFromSchema = false)
    {
        ClaimType = null;
        IsRequired = isRequired;
        RemoveFromSchema = removeFromSchema;
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="claimType">optionally specify the claim type to bind from. if not specified, the claim type of the user principal must match the name of the property being bound to.</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user principal doesn't have the specified claim type</param>
    /// <param name="removeFromSchema">set to true if your header is not required but shouldn't be added to schema model.</param>
    public FromClaimAttribute(string? claimType = null, bool isRequired = true, bool removeFromSchema = false)
    {
        ClaimType = claimType;
        IsRequired = isRequired;
        RemoveFromSchema = removeFromSchema;
    }
}
