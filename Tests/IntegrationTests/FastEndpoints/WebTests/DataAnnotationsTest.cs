﻿using System.Net;
using TestCases.DataAnnotationCompliant;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Int.FastEndpoints.WebTests;

[DisableWafCache]
public class DaFixture : AppFixture<Web.Program>;

public class DataAnnotationsTest(DaFixture App) : TestBase<DaFixture>
{
    [Fact]
    public async Task WithBadInput()
    {
        var (rsp, res) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 10,
                    Name = "x"
                });

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.Count.ShouldBe(2);
        res.Errors.Keys.ShouldBe(["id", "name"]);
    }

    [Fact]
    public async Task WithOkInput()
    {
        var (resp, _) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 100,
                    Name = "pass"
                });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}