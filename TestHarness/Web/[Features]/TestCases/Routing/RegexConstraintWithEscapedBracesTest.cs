namespace TestCases.Routing;

public class RegexConstraintWithEscapedBracesTest : Ep
    .Req<RegexConstraintWithEscapedBracesTest.Request>
    .Res<string>
{
    public override void Configure()
    {
        //the {{3}} quantifier below is ASP.NET Core route-template escaping for a literal "{3}"
        //inside the regex constraint - i.e. this matches exactly 3 digits.
        Put("test-cases/routing/regexconstraint/{Code:regex(^\\d{{3}}$)}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync(req.Code);
    }

    public record Request(string Code);
}
