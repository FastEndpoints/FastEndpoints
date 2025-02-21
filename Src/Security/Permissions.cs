using System.Collections;
using System.Reflection;

#pragma warning disable CS8618,CA1822

namespace FastEndpoints.Security;

/// <summary>
/// inherit from this class and define your applications permissions as <c>public const string</c>
/// <para>
///     <code>
/// public const string Inventory_Create_Item = "100";
/// public const string Inventory_Retrieve_Item = "101";
/// public const string Inventory_Update_Item = "102";
/// public const string Inventory_Delete_Item = "103";
/// </code>
/// </para>
/// </summary>
public abstract class Permissions : IEnumerable<(string PermissionName, string PermissionCode)>
{
    static bool _isInitialized;

    static IEnumerable<(string PermissionName, string PermissionCode)> _permissions = [];

    protected Permissions()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;

            _permissions = GetType()
                           .GetFields(BindingFlags.Public | BindingFlags.Static)
                           .Select(f => (f.Name, (string)f.GetValue(this)!))
                           .ToArray();
        }
    }

    /// <summary>
    /// gets a list of permission names for the given list of permission codes
    /// </summary>
    /// <param name="codes">the permission codes to get the permission names for</param>
    public IEnumerable<string> NamesFor(IEnumerable<string> codes)
    {
        return _permissions
               .Where(f => codes.Contains(f.PermissionCode))
               .Select(f => f.PermissionName);
    }

    /// <summary>
    /// get a list of permission codes for a given list of permission names
    /// </summary>
    /// <param name="names">the permission names to get the codes for</param>
    public IEnumerable<string> CodesFor(IEnumerable<string> names)
    {
        return _permissions
               .Where(f => names.Contains(f.PermissionName))
               .Select(f => f.PermissionCode);
    }

    /// <summary>
    /// get the permission tuple using it's name. returns null if not found
    /// </summary>
    /// <param name="permissionName">name of the permission</param>
    public (string PermissionName, string PermissionCode)? PermissionFromName(string permissionName)
        => _permissions.SingleOrDefault(p => p.PermissionName == permissionName);

    /// <summary>
    /// get the permission tuple using it's code. returns null if not found
    /// </summary>
    /// <param name="permissionCode">code of the permission to get</param>
    public (string PermissionName, string PermissionCode)? PermissionFromCode(string permissionCode)
        => _permissions.SingleOrDefault(p => p.PermissionCode == permissionCode);

    /// <summary>
    /// get a list of all permission names
    /// </summary>
    public IEnumerable<string> AllNames()
        => _permissions.Select(f => f.PermissionName);

    /// <summary>
    /// get a list of all permission codes
    /// </summary>
    public IEnumerable<string> AllCodes()
        => _permissions.Select(f => f.PermissionCode);

    /// <inheritdoc />
    public IEnumerator<(string PermissionName, string PermissionCode)> GetEnumerator()
        => _permissions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}