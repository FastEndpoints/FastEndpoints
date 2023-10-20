using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;


using DataAnnotationCompliant = TestCases.DataAnnotationCompliant;

namespace Int.FastEndpoints.WebTests;


public class DataAnnotationCompliantTest : TestClass<Fixture>
{
    public DataAnnotationCompliantTest(Fixture f, ITestOutputHelper o) : base(f, o) { }


    [Fact]
    [Obsolete]
    public async Task WithBadInput()
    {
        var (resp, result) = await Fixture.GuestClient.
            POSTAsync<DataAnnotationCompliant.Endpoint, DataAnnotationCompliant.Request, ErrorResponse>(new()
            {
                Id = 199,
                Name = "x",
            });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Errors.Count.Should().Be(2);
    }

    [Fact]
    [Obsolete]
    public async Task WithOkInput()
    {
        var (resp, result) = await Fixture.GuestClient.
            POSTAsync<DataAnnotationCompliant.Endpoint, DataAnnotationCompliant.Request, ErrorResponse>(new()
            {
                Id = 10,
                Name = "vipwan",
            });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

}