using FastEndpoints;

namespace Admin.Login
{
    public class Validator : FluentValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("Username is required!")
                .MinimumLength(3).WithMessage("Username too short!");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required!")
                .MinimumLength(3).WithMessage("Password too short!");
        }
    }
}