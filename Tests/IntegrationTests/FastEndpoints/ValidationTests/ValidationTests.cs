using System.Net;
using Microsoft.AspNetCore.Http;

namespace Validation;

public class ValidationTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task HeaderMissing()
    {
        var (_, result) = await Fixture.AdminClient.POSTAsync<
                              TestCases.MissingHeaderTest.ThrowIfMissingEndpoint,
                              TestCases.MissingHeaderTest.ThrowIfMissingRequest,
                              ErrorResponse>(
                              new()
                              {
                                  TenantID = "abc"
                              });

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Errors.Should().NotBeNull();
        result.Errors.Count.Should().Be(1);
        result.Errors.Should().ContainKey("TenantID");
    }

    [Fact]
    public async Task HeaderMissingButDontThrow()
    {
        var (res, result) = await Fixture.AdminClient.POSTAsync<
                                TestCases.MissingHeaderTest.DontThrowIfMissingEndpoint,
                                TestCases.MissingHeaderTest.DontThrowIfMissingRequest,
                                string>(
                                new()
                                {
                                    TenantID = "abc"
                                });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent abc");
    }
}