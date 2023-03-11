namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal.
/// this is a shorter alias for the [FromClaim] attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FromAttribute : FromClaimAttribute
{
    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal.
    /// this is a shorter alias for the [FromClaim] attribute.
    /// </summary>
    /// <param name="claimType">the claim type to auto bind</param>
    /// <param name="isRequired">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
    public FromAttribute(string claimType, bool isRequired = true) : base(claimType, isRequired) { }
}
