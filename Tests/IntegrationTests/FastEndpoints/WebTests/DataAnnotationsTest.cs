using System.Net;
using TestCases.DataAnnotationCompliant;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Int.FastEndpoints.WebTests;

public class DaFixture(IMessageSink s) : AppFixture<Web.Program>(s);

public class DataAnnotationsTest(DaFixture f, ITestOutputHelper o) : TestClass<DaFixture>(f, o)
{
    [Fact]
    public async Task WithBadInput()
    {
        var (rsp, res) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 199,
                    Name = "x"
                });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Count.Should().Be(2);
        res.Errors.Should().ContainKey("Name");
    }

    [Fact]
    public async Task WithOkInput()
    {
        var (resp, _) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 10,
                    Name = "vipwan"
                });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}