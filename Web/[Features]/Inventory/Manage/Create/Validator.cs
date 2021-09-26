using FastEndpoints;

namespace Inventory.Manage.Create
{
    public class Validator : FluentValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required!");
            RuleFor(x => x.Price).GreaterThan(0).WithMessage("Product price is required!");
            RuleFor(x => x.ModifiedBy).NotEmpty().WithMessage("ModifiedBy is required!");
        }
    }
}