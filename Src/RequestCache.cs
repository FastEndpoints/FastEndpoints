using System.Reflection;

namespace EZEndpoints
{
    internal static class RequestCache<T>
    {
        internal static PropertyInfo[]? Props { get; }
        internal static Dictionary<string, (string claimType, PropertyInfo propInfo)> FromClaimProps { get; } = new();
        internal static Dictionary<string, PropertyInfo> FromRouteProps { get; } = new();

        static RequestCache()
        {
            Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.FlattenHierarchy);

            for (int i = 0; i < Props.Length; i++)
            {
                PropertyInfo? p = Props[i];

                if (p.IsDefined(typeof(FromClaimAttribute), false))
                {
                    var claimType = p.GetCustomAttribute<FromClaimAttribute>(false)?.ClaimType;

                    FromClaimProps.Add(
                        p.Name,
                        new(claimType ?? "", p));
                }

                if (p.IsDefined(typeof(FromRouteAttribute), true))
                    FromRouteProps.Add(p.Name, p);
            }
        }
    }
}
