namespace Inventory.Manage.Update;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes("/inventory/manage/update");
        Policies("AdminOnly");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Update_Item);
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        //this is a test case for checking security policy restrictions
        return SendOkAsync();
    }
}
