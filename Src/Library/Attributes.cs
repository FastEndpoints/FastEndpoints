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
    public string ClaimType { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current user principal doesn't have the specified claim
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="claimType">the claim type to auto bind</param>
    /// <param name="isRequired">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
    public FromClaimAttribute(string claimType, bool isRequired = true)
    {
        ClaimType = claimType;
        IsRequired = isRequired;
    }
}

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromAttribute : FromClaimAttribute
{
    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="claimType">the claim type to auto bind</param>
    /// <param name="isRequired">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
    public FromAttribute(string claimType, bool isRequired = true) : base(claimType, isRequired) { }
}

/// <summary>
/// attribute used to mark classes that should be hidden from public api
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false), HideFromDocs]
public class HideFromDocsAttribute : Attribute { }
