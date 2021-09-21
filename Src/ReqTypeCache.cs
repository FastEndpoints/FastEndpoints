using System.Reflection;

namespace EZEndpoints
{
    internal static class ReqTypeCache<T>
    {
        internal static Dictionary<string, PropertyInfo> Props { get; } = new();
        internal static Dictionary<string, (string claimType, PropertyInfo propInfo)> FromClaimProps { get; } = new();

        static ReqTypeCache()
        {
            foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var name = p.Name.ToLower();

                Props.Add(name, p);

                if (p.IsDefined(typeof(FromClaimAttribute), false))
                {
                    var claimType = p.GetCustomAttribute<FromClaimAttribute>(false)?.ClaimType;

                    FromClaimProps.Add(
                        name, new(claimType ?? "null", p));
                }
            }
        }
    }
}
