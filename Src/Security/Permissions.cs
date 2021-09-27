using System.Collections;
using System.Reflection;

namespace FastEndpoints.Security
{
    public class Permissions : IEnumerable<(string PermissionName, string PermissionCode)>
    {
#pragma warning disable CS8618
        private static bool isInitialized;
        private static IEnumerable<(string PermissionName, string PermissionCode)> fields;
#pragma warning restore CS8618

        public Permissions()
        {
            if (!isInitialized)
            {
                isInitialized = true;

#pragma warning disable CS8619, CS8600
                fields = GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(f => (f.Name, (string)f.GetValue(this)))
                    .ToArray();
#pragma warning restore CS8600, CS8619
            }
        }

#pragma warning disable CA1822
        public IEnumerable<string> NamesFor(IEnumerable<string> codes)
        {
            return fields
                .Where(f => codes.Contains(f.PermissionCode))
                .Select(f => f.PermissionName);
        }

        public IEnumerable<string> CodesFor(IEnumerable<string> names)
        {
            return fields
                .Where(f => names.Contains(f.PermissionName))
                .Select(f => f.PermissionCode);
        }
#pragma warning restore CA1822

        public IEnumerator<(string PermissionName, string PermissionCode)> GetEnumerator()
        {
            return fields.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
