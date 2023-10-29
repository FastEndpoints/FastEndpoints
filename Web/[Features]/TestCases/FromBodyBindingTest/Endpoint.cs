using FluentValidation;

namespace TestCases.FromBodyJsonBinding;

public class Product
{
    /// <summary>
    /// product id goes here
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// this is the name of the product
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// product price goes here
    /// </summary>
    public decimal Price { get; set; }
}

public class Request
{
    public int Id { get; set; }

    [FromBody]
    public Product Product { get; set; }

    [FromHeader]
    public int CustomerID { get; set; }

    public sealed class Validator : Validator<Request>
    {
        public Validator() { RuleFor(x => x.Product.Price).GreaterThan(200); }
    }
}

public class Response : Request { }

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("test-cases/from-body-binding/{id}");
        AllowAnonymous();
        Summary(
            s => s.ExampleRequest = new Product
            {
                Id = 201,
                Name = "test product name",
                Price = 200.22m
            });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await SendAsync(
            new()
            {
                Id = req.Id,
                CustomerID = req.CustomerID,
                Product = req.Product
            });
    }
}