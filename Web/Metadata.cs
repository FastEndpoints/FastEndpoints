using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Int.FastEndpoints")]
[assembly: InternalsVisibleTo("Int.Swagger")]
[assembly: InternalsVisibleTo("Unit.FastEndpoints")]

public static partial class Allow_
{

    #region ACL_ITEMS
    /// <summary><see cref="Customers.Create.Endpoint"/></summary>
    public const string Customers_Create = "FTQ";

    /// <summary><see cref="Customers.CreateWithPropertiesDI.Endpoint"/></summary>
    public const string Customers_Create_2 = "B6R";

    /// <summary><see cref="Customers.List.Recent.Endpoint"/></summary>
    public const string Customers_Retrieve = "UUH";

    /// <summary><see cref="Customers.Update.Endpoint"/></summary>
    public const string Customers_Update = "GR2";

    /// <summary><see cref="Uploads.Image.SaveTyped.Endpoint"/></summary>
    public const string Image_Update = "O2O";

    /// <summary><see cref="Inventory.Manage.Create.Endpoint"/></summary>
    public const string Inventory_Create_Item = "3YI";

    /// <summary><see cref="Inventory.Manage.Delete.Endpoint"/></summary>
    public const string Inventory_Delete_Item = "PIZ";

    /// <summary><see cref="Inventory.GetProduct.Endpoint"/></summary>
    public const string Inventory_Retrieve_Item = "IBU";

    /// <summary><see cref="Inventory.Manage.Update.Endpoint"/></summary>
    public const string Inventory_Update_Item = "NLM";

    /// <summary><see cref="Sales.Orders.Create.Endpoint"/></summary>
    public const string Sales_Order_Create = "LYO";
    #endregion

    #region GROUPS
    public static string[] Admin { get; } =
    {
        Customers_Create,
        Inventory_Create_Item
    };
    #endregion

    private static readonly Dictionary<string, string> _perms = new();
    private static readonly Dictionary<string, string> _permsReverse = new();

    static Allow_()
    {
        foreach (var f in typeof(Allow).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var val = f.GetValue(null)?.ToString() ?? string.Empty;
            _perms[f.Name] = val;
            _permsReverse[val] = f.Name;
        }
    }

    /// <summary>
    /// gets a list of permission names for the given list of permission codes
    /// </summary>
    /// <param name="codes">the permission codes to get the permission names for</param>
    public static IEnumerable<string> NamesFor(IEnumerable<string> codes)
        => _perms.Where(kv => codes.Contains(kv.Value)).Select(kv => kv.Key);

    /// <summary>
    /// get a list of permission codes for a given list of permission names
    /// </summary>
    /// <param name="names">the permission names to get the codes for</param>
    public static IEnumerable<string> CodesFor(IEnumerable<string> names)
        => _perms.Where(kv => names.Contains(kv.Key)).Select(kv => kv.Value);

    /// <summary>
    /// get the permission code for a given permission name
    /// </summary>
    /// <param name="permissionName">the name of the permission to get the code for</param>
    public static string? PermissionCodeFor(string permissionName)
    {
        if (_perms.TryGetValue(permissionName, out var code))
            return code;
        return null;
    }

    /// <summary>
    /// get the permission name for a given permission code
    /// </summary>
    /// <param name="permissionCode">the permission code to get the name for</param>
    public static string? PermissionNameFor(string permissionCode)
    {
        if (_permsReverse.TryGetValue(permissionCode, out var name))
            return name;
        return null;
    }

    /// <summary>
    /// get a permission tuple using it's name. returns null if not found
    /// </summary>
    /// <param name="permissionName">name of the permission</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromName(string permissionName)
    {
        if (_perms.TryGetValue(permissionName, out var code))
            return new(permissionName, code);
        return null;
    }

    /// <summary>
    /// get the permission tuple using it's code. returns null if not found
    /// </summary>
    /// <param name="permissionCode">code of the permission to get</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromCode(string permissionCode)
    {
        if (_permsReverse.TryGetValue(permissionCode, out var name))
            return new(name, permissionCode);
        return null;
    }

    /// <summary>
    /// get a list of all permission names
    /// </summary>
    public static IEnumerable<string> AllNames()
        => _perms.Keys;

    /// <summary>
    /// get a list of all permission codes
    /// </summary>
    public static IEnumerable<string> AllCodes()
        => _perms.Values;

    /// <summary>
    /// get a list of all the defined permissions
    /// </summary>
    public static IEnumerable<(string PermissionName, string PermissionCode)> AllPermissions()
        => _perms.Select(kv => new ValueTuple<string, string>(kv.Key, kv.Value));
}