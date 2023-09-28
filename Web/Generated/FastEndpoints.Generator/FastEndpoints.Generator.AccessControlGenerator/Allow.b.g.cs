#nullable enable

using System.Reflection;

namespace Web.Auth;

public static partial class Allow
{
    private static readonly Dictionary<string, string> _permNames = new();
    private static readonly Dictionary<string, string> _permCodes = new();

    static Allow()
    {
        foreach (var f in typeof(Allow).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var val = f.GetValue(null)?.ToString() ?? string.Empty;
            _permNames[f.Name] = val;
            _permCodes[val] = f.Name;
        }
        Groups();
    }

    /// <summary>
    /// implement this method to add custom permissions to the generated categories
    /// </summary>
    static partial void Groups();

    /// <summary>
    /// gets a list of permission names for the given list of permission codes
    /// </summary>
    /// <param name="codes">the permission codes to get the permission names for</param>
    public static IEnumerable<string> NamesFor(IEnumerable<string> codes)
    {
        foreach (var code in codes)
            if (_permCodes.TryGetValue(code, out var name)) yield return name;
    }

    /// <summary>
    /// get a list of permission codes for a given list of permission names
    /// </summary>
    /// <param name="names">the permission names to get the codes for</param>
    public static IEnumerable<string> CodesFor(IEnumerable<string> names)
    {
        foreach (var name in names)
            if (_permNames.TryGetValue(name, out var code)) yield return code;
    }

    /// <summary>
    /// get the permission code for a given permission name
    /// </summary>
    /// <param name="permissionName">the name of the permission to get the code for</param>
    public static string? PermissionCodeFor(string permissionName)
    {
        if (_permNames.TryGetValue(permissionName, out var code))
            return code;
        return null;
    }

    /// <summary>
    /// get the permission name for a given permission code
    /// </summary>
    /// <param name="permissionCode">the permission code to get the name for</param>
    public static string? PermissionNameFor(string permissionCode)
    {
        if (_permCodes.TryGetValue(permissionCode, out var name))
            return name;
        return null;
    }

    /// <summary>
    /// get a permission tuple using it's name. returns null if not found
    /// </summary>
    /// <param name="permissionName">name of the permission</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromName(string permissionName)
    {
        if (_permNames.TryGetValue(permissionName, out var code))
            return new(permissionName, code);
        return null;
    }

    /// <summary>
    /// get the permission tuple using it's code. returns null if not found
    /// </summary>
    /// <param name="permissionCode">code of the permission to get</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromCode(string permissionCode)
    {
        if (_permCodes.TryGetValue(permissionCode, out var name))
            return new(name, permissionCode);
        return null;
    }

    /// <summary>
    /// get a list of all permission names
    /// </summary>
    public static IEnumerable<string> AllNames()
        => _permNames.Keys;

    /// <summary>
    /// get a list of all permission codes
    /// </summary>
    public static IEnumerable<string> AllCodes()
        => _permNames.Values;

    /// <summary>
    /// get a list of all the defined permissions
    /// </summary>
    public static IEnumerable<(string PermissionName, string PermissionCode)> AllPermissions()
        => _permNames.Select(kv => new ValueTuple<string, string>(kv.Key, kv.Value));
}