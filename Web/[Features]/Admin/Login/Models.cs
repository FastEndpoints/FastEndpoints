using System.Text.Json.Serialization;

namespace Admin.Login;

[JsonSerializable(typeof(Request))]
public partial class ReqJsonCtx : JsonSerializerContext { }
public class Request
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string GetterOnlyProp => "test";
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

[JsonSerializable(typeof(Response))]
public partial class ResJsonCtx : JsonSerializerContext { }
public class Response
{
    public string? JWTToken { get; set; }
    public DateTime ExpiryDate { get; set; }
    public IEnumerable<string>? Permissions { get; set; }
}