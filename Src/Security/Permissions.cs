using System.Collections;
using System.Reflection;

namespace FastEndpoints.Security;

/// <summary>
/// inherit from this class and define your applications permissions as <c>public const string</c>
/// <para>
/// <code>
/// public const string Inventory_Create_Item = "100";
/// public const string Inventory_Retrieve_Item = "101";
/// public const string Inventory_Update_Item = "102";
/// public const string Inventory_Delete_Item = "103";
/// </code>
/// </para>
/// </summary>
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
    /// <summary>
    /// gets a list of permission names for the given list of permission codes
    /// </summary>
    /// <param name="codes">the permission codes to get the permission names for</param>
    public IEnumerable<string> NamesFor(IEnumerable<string> codes)
    {
        return fields
            .Where(f => codes.Contains(f.PermissionCode))
            .Select(f => f.PermissionName);
    }

    /// <summary>
    /// get a list of permission codes for a given list of permission names
    /// </summary>
    /// <param name="names">the permission names to get the codes for</param>
    public IEnumerable<string> CodesFor(IEnumerable<string> names)
    {
        return fields
            .Where(f => names.Contains(f.PermissionName))
            .Select(f => f.PermissionCode);
    }
#pragma warning restore CA1822

    /// <inheritdoc/>
    public IEnumerator<(string PermissionName, string PermissionCode)> GetEnumerator()
    {
        return fields.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

