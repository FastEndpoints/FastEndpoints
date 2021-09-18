using FluentValidation;

namespace Inventory.GetList.NewlyAdded
{
    public class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required!");

            RuleFor(x => x.Price)
                .GreaterThan(100).WithMessage("Price is too low!")
                .NotEqual(100).WithMessage("200 is not allowed!");
        }
    }
}
