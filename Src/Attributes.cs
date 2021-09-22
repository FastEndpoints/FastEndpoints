namespace ApiExpress
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FromClaimAttribute : Attribute
    {
        public string ClaimType { get; set; }

        public FromClaimAttribute(string claimType)
            => ClaimType = claimType;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FromRouteAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class FromServicesAttribute : Attribute { }
}
