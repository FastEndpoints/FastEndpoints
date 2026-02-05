using System.Net;
using NativeAotChecker.Endpoints.Validation;

namespace NativeAotCheckerTests;

public class ValidationTests(App app)
{
    [Fact]
    public async Task FluentValidation_Validator()
    {
        var (rsp, res) = await app.Client.POSTAsync<FluentValidationEndpoint, FluentValidationRequest, ErrorResponse>(
                             new()
                             {
                                 Email = "",
                                 FullName = "AB",
                                 Age = 0
                             });

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        res.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
        res.Errors.Keys.ShouldContain("email");
        res.Errors.Keys.ShouldContain("full_name");
        res.Errors.Keys.ShouldContain("age");

        res.Errors["email"].ShouldContain("Email is required!");
        res.Errors["full_name"].ShouldContain("Full name must be at least 3 characters!");
        res.Errors["age"].ShouldContain("Age must be greater than 0!");
    }

    [Fact]
    public async Task Error_Response_With_Property_Expression_Use()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<ErrorWithPropertyExpressionEndpoint, ErrorWithPropertyExpressionRequest, ErrorResponse>(
                                  new()
                                  {
                                      Items = ["123", "321"]
                                  });

        if (rsp.StatusCode != HttpStatusCode.BadRequest)
            Assert.Fail(err);

        res.Errors.Count.ShouldBe(1);
        res.Errors.Keys.ShouldContain("items[1]");
    }
}
