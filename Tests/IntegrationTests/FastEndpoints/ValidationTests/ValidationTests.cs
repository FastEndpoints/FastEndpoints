using System.Net;
using Microsoft.AspNetCore.Http;

namespace Validation;

public class ValidationTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task HeaderMissing()
    {
        var (_, result) = await App.AdminClient.POSTAsync<
                              TestCases.MissingHeaderTest.ThrowIfMissingEndpoint,
                              TestCases.MissingHeaderTest.ThrowIfMissingRequest,
                              ErrorResponse>(
                              new()
                              {
                                  TenantID = "abc"
                              });

        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.Errors.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldContainKey("tenantID");
    }

    [Fact]
    public async Task HeaderMissingButDontThrow()
    {
        var (res, result) = await App.AdminClient.POSTAsync<
                                TestCases.MissingHeaderTest.DontThrowIfMissingEndpoint,
                                TestCases.MissingHeaderTest.DontThrowIfMissingRequest,
                                string>(
                                new()
                                {
                                    TenantID = "abc"
                                });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldBe("you sent abc");
    }
}