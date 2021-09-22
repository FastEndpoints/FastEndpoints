using System.Reflection;

namespace ApiExpress
{
    internal static class ReqTypeCache<T> where T : IRequest
    {
        internal static Dictionary<string, PropertyInfo> Props { get; } = new();
        internal static Dictionary<string, (string claimType, bool forbidIfMissing, PropertyInfo propInfo)> FromClaimProps { get; } = new();

        static ReqTypeCache()
        {
            foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var name = p.Name.ToLower();

                Props.Add(name, p);

                if (p.IsDefined(typeof(FromClaimAttribute), false))
                {
                    var attrib = p.GetCustomAttribute<FromClaimAttribute>(false);
                    var claimType = attrib?.ClaimType ?? "null";
                    var forbidIfMissing = attrib?.ForbidIfMissing ?? false;

                    FromClaimProps.Add(name, new(claimType, forbidIfMissing, p));
                }
            }
        }
    }
}
