using FluentValidation;
using System.ComponentModel;
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
    /// <example>example username</example>
    [DefaultValue("admin")]
    public string UserName { get; set; }

    /// <summary>
    /// the admin password
    /// </summary>
    /// <example>example password</example>
    public string Password { get; set; }

    [JsonIgnore]
    public string GetterOnlyProp => "test";
}

public class Validator : Validator<Request>
{
    public Validator(IConfiguration config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        RuleFor(x => x.UserName)
            .Must(_ => config["TokenKey"] == "some long secret key to sign jwt tokens with")
            .WithMessage("config didn't resolve correctly!")
            .MaximumLength(50)
            .WithMessage("cannot exceed 50 chars!");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required!")
            .MinimumLength(3).WithMessage("Username too short!");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required!")
            .MinimumLength(3).WithMessage("Password too short!");

        var logger = Resolve<ILogger<Validator>>();
        logger.LogError("resolving from validator works!");
    }
}

public class Response
{
    /// <summary>
    /// the jwt token
    /// </summary>
    /// <example>blahblahblah</example>
    public string JWTToken { get; set; }
    public DateTime ExpiryDate { get; set; }
    public IEnumerable<string> Permissions { get; set; }
}