using System.Reflection;

namespace ApiExpress
{
    internal static class ReqTypeCache<T> where T : IRequest
    {
        internal static Dictionary<string, PropertyInfo> Props { get; } = new();
        internal static Dictionary<string, (string claimType, bool forbidIfMissing, PropertyInfo propInfo)> FromClaimProps { get; } = new();

        static ReqTypeCache()
        {
            foreach (var propInfo in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var propName = propInfo.Name.ToLower();

                Props.Add(propName, propInfo);

                if (propInfo.IsDefined(typeof(FromClaimAttribute), false))
                {
                    if (propInfo.PropertyType != typeof(string))
                        throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");

                    var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
                    var claimType = attrib?.ClaimType ?? "null";
                    var forbidIfMissing = attrib?.ForbidIfMissing ?? false;

                    FromClaimProps.Add(propName, new(claimType, forbidIfMissing, propInfo));
                }
            }
        }
    }
}
