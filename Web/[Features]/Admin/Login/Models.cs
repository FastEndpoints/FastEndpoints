using System.Text.Json.Serialization;

namespace Admin.Login;

[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(Response))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class AdminLogin : JsonSerializerContext { }

/// <summary>
/// the admin login request
/// </summary>
public class Request
{
    /// <summary>
    /// the admin username
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// the admin password
    /// </summary>
    public string Password { get; set; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore]
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

public class Response
{
    public string JWTToken { get; set; }
    public DateTime ExpiryDate { get; set; }
    public IEnumerable<string> Permissions { get; set; }
}