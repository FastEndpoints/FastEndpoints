﻿using System.Net;
using TestCases.DataAnnotationCompliant;

namespace Int.FastEndpoints.WebTests;

public class DataAnnotationsTest : TestClass<Fixture>
{
    public DataAnnotationsTest(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task WithBadInput()
    {
        var (rsp, res) =
            await Fixture.GuestClient.POSTAsync<Endpoint, Request, ErrorResponse>(
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
            await Fixture.GuestClient.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 10,
                    Name = "vipwan"
                });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}