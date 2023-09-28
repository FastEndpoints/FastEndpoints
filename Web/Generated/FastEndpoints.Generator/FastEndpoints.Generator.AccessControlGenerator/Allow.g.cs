#nullable enable

namespace Web.Auth;

public static partial class Allow
{

#region ACL_ITEMS
    /// <summary><see cref="Customers.Create.Endpoint"/></summary>
    public const string Customers_Create = "JHH";

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
    public static IEnumerable<string> Admin => _admin;
    private static void AddToAdmin(string permissionCode) => _admin.Add(permissionCode);
    private static readonly List<string> _admin = new()
    {
        Customers_Create,
        Customers_Retrieve,
        Customers_Update,
        Inventory_Create_Item,
        Inventory_Delete_Item,
        Inventory_Retrieve_Item,
        Inventory_Update_Item,
    };

    public static IEnumerable<string> Manager => _manager;
    private static void AddToManager(string permissionCode) => _manager.Add(permissionCode);
    private static readonly List<string> _manager = new()
    {
        Customers_Create,
    };
#endregion
}