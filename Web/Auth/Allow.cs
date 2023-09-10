namespace Auth;

public static partial class Allow
{
    public const string Additional_Permission = "_AP1";
    public const string Another_Permission = "_AP2";

    static partial void Groups()
    {
        AddToAdmin(Additional_Permission);
        AddToManager(Another_Permission);
    }
}