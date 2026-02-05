using System.Diagnostics.CodeAnalysis;

namespace NativeAotChecker.Endpoints.Binding;

public class NonJsonRequest
{
    [RouteParam]
    public string Id { get; set; }

    [QueryParam]
    public Guid Identifier { get; set; }

    [FromHeader]
    public UserName UserName { get; set; }
}

public class NonJsonResponse
{
    public string Id { get; set; }
    public Guid Identifier { get; set; }
    public string? UserName { get; set; }
}

public class UserName : IParsable<UserName>
{
    public string? Value { get; set; }

    public override string ToString()
        => Value ?? string.Empty;

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out UserName result)
    {
        result = new() { Value = s };

        return result.Value is not null;
    }

    public static UserName Parse(string s, IFormatProvider? provider)
        => throw new NotImplementedException();
}

public class NonJsonBindingEndpoint : Endpoint<NonJsonRequest, NonJsonResponse>
{
    public override void Configure()
    {
        Get("non-json-binding-endpoint/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(NonJsonRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Id = req.Id,
                Identifier = req.Identifier,
                UserName = req.UserName.Value
            });
    }
}