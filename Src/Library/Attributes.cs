namespace FastEndpoints
{
    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FromClaimAttribute : Attribute
    {
        /// <summary>
        /// the claim type to auto bind
        /// </summary>
        public string ClaimType { get; set; }

        /// <summary>
        /// set to true if a validation error should be thrown when the current user principal doesn't have the specified claim
        /// </summary>
        public bool ForbidIfMissing { get; set; }

        /// <summary>
        /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
        /// </summary>
        /// <param name="claimType">the claim type to auto bind</param>
        /// <param name="forbidIfMissing">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
        public FromClaimAttribute(string claimType, bool forbidIfMissing = true)
        {
            ClaimType = claimType;
            ForbidIfMissing = forbidIfMissing;
        }
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FromAttribute : FromClaimAttribute
    {
        /// <summary>
        /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
        /// </summary>
        /// <param name="claimType">the claim type to auto bind</param>
        /// <param name="forbidIfMissing">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
        public FromAttribute(string claimType, bool forbidIfMissing = true) : base(claimType, forbidIfMissing) { }
    }
}