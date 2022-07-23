using System.Text.Json.Serialization;

namespace TestCases.FromBodyJsonBinding;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

public class Request
{
    [JsonIgnore]
    public int Id { get; set; }

    [FromBody]
    public Product Product { get; set; }

    [FromHeader]
    public int CustomerID { get; set; }
}

public class Response : Request { }

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
        => Post("test-cases/from-body-binding/{id}");

    public async override Task HandleAsync(Request req, CancellationToken ct)
    {
        await SendAsync(new Response
        {
            CustomerID = req.CustomerID,
            Id = req.Id,
            Product = req.Product,
        });
    }
}