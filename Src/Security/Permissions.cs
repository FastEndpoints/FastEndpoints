using System.Collections;
using System.Reflection;

namespace ApiExpress.Security
{
    public class Permissions : IEnumerable<(string PermissionName, string PermissionCode)>
    {
        private static bool isInitialized;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static IEnumerable<(string PermissionName, string PermissionCode)> fields;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public Permissions()
        {
            if (!isInitialized)
            {
                isInitialized = true;

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

                fields = GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(f => (f.Name, (string)f.GetValue(this)))
                    .ToArray();

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
            }
        }

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
