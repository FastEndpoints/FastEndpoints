using FluentValidation;

namespace Inventory.Manage.Create;

public class Request
{
    [From(Claim.AdminID)]
    public string? UserID { get; set; }

    [FromClaim]
    public string? Username { get; set; }

    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int QtyOnHand { get; set; }
    public string? ModifiedBy { get; set; }

    public bool GenerateFullUrl { get; set; }
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required!");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Product price is required!");
        RuleFor(x => x.ModifiedBy).NotEmpty().WithMessage("ModifiedBy is required!");
    }
}

public class Response
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
}