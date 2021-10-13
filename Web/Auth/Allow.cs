namespace Web.Auth;

public class Allow : Permissions
{
    public const string Inventory_Create_Item = "100";
    public const string Inventory_Retrieve_Item = "101";
    public const string Inventory_Update_Item = "102";
    public const string Inventory_Delete_Item = "103";

    public const string Customers_Retrieve = "200";
    public const string Customers_Create = "201";
    public const string Customers_Update = "202";
    public const string Customers_Delete = "201";
}
