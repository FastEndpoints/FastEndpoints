namespace Inventory.Manage.Delete;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("inventory/manage/delete/{itemID}");
        AccessControl("Inventory_Delete_Item", "Admin");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return Send.OkAsync();
    }
}