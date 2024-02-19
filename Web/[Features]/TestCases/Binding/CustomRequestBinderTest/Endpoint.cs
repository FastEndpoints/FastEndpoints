namespace TestCases.CustomRequestBinder;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/test-cases/custom-request-binder");
        RequestBinder(new Binder());
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(new()
        {
            Id = r.Id,
            CustomerID = r.CustomerID,
            Product = r.Product
        });
    }
}