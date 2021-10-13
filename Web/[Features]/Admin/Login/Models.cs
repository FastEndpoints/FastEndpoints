namespace Admin.Login;

public class Request
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

public class Validator : Validator<Request>
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

public class Response
{
    public string? JWTToken { get; set; }
    public DateTime ExpiryDate { get; set; }
    public IEnumerable<string>? Permissions { get; set; }
}

