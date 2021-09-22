namespace ApiExpress
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FromClaimAttribute : Attribute
    {
        public string ClaimType { get; set; }
        public bool ForbidIfMissing { get; set; }

        public FromClaimAttribute(string claimType, bool forbidIfMissing = true)
        {
            ClaimType = claimType;
            ForbidIfMissing = forbidIfMissing;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FromAttribute : FromClaimAttribute
    {
        public FromAttribute(string claimType, bool forbidIfMissing = true) : base(claimType, forbidIfMissing) { }
    }
}