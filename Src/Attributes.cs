namespace ApiExpress
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FromClaimAttribute : Attribute
    {
        public string ClaimType { get; set; }
        public bool ForbidIfMissing { get; set; }

        public FromClaimAttribute(string claimType, bool forbidIfMissing)
        {
            ClaimType = claimType;
            ForbidIfMissing = forbidIfMissing;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FromRouteAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class FromServicesAttribute : Attribute { }
}
