namespace Inventory.Manage.Delete;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("inventory/manage/delete/{ItemID}");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendOkAsync();
    }
}