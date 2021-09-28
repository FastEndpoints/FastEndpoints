using System.Reflection;

namespace FastEndpoints
{
    internal static class ReqTypeCache<TRequest>
    {
        //note: key is lowercased property name
        internal static Dictionary<string, (PropertyInfo propInfo, TypeCode typeCode)> Props { get; } = new();

        internal static List<(string claimType, bool forbidIfMissing, PropertyInfo propInfo)> FromClaimProps { get; } = new();

        static ReqTypeCache()
        {
            foreach (var propInfo in typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                var propName = propInfo.Name.ToLower();

                Props.Add(propName, (propInfo, Type.GetTypeCode(propInfo.PropertyType)));

                if (propInfo.IsDefined(typeof(FromClaimAttribute), false))
                {
                    if (propInfo.PropertyType != typeof(string))
                        throw new InvalidOperationException("[FromClaim] attributes are only supported on string properties!");
                    //todo: add claim binding support for other types. same as route binding.

                    var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
                    var claimType = attrib?.ClaimType ?? "null";
                    var forbidIfMissing = attrib?.ForbidIfMissing ?? false;

                    FromClaimProps.Add((claimType, forbidIfMissing, propInfo));
                }
            }
        }
    }
}
