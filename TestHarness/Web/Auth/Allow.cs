namespace Web.Auth;

public static partial class Allow
{
    public const string Additional_Permission = "_AP1";
    public const string Another_Permission = "_AP2";

    static partial void Groups()
    {
        AddToAdmin(Additional_Permission);
        AddToManager(Another_Permission);
    }

    static partial void Describe()
    {
        Descriptions[Additional_Permission] = "Description for first custom permission";
        Descriptions[Another_Permission] = "Another custom permission";
        Descriptions[Inventory_Create_Item] = "Descriptions for generated permission";
    }
}