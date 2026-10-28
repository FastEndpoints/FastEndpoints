using System.Net;
using TestCases.DataAnnotationCompliant;
using Services = TestCases.DataAnnotationServices;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Int.FastEndpoints.WebTests;

[DisableWafCache]
public class DaFixture : AppFixture<Web.Program>;

public class DataAnnotationsTest(DaFixture App) : TestBase<DaFixture>
{
    [Fact]
    public async Task CustomAttributesUseTheRequestScopeForTheEntireObjectGraph()
    {
        var (firstResponse, first) = await App.Client.POSTAsync<Services.Endpoint, Services.Request, Services.Response>(new());
        var (secondResponse, second) = await App.Client.POSTAsync<Services.Endpoint, Services.Request, Services.Response>(new());

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.ValidatedValues.Order().ShouldBe(new[] { "root", "nested", "child" }.Order());
        second.ValidatedValues.Order().ShouldBe(first.ValidatedValues.Order());
        first.ScopeId.ShouldNotBe(Guid.Empty);
        second.ScopeId.ShouldNotBe(first.ScopeId);
    }

    [Fact]
    public async Task CustomAttributeFailuresPreserveNestedPropertyPaths()
    {
        var (response, result) = await App.Client.POSTAsync<Services.Endpoint, Services.Request, ErrorResponse>(
                                    new()
                                    {
                                        Value = "invalid",
                                        Nested = new() { Value = "invalid" },
                                        Children = [new() { Value = "invalid" }]
                                    });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Errors.Keys.Order().ShouldBe(new[] { "value", "nested.Value", "children[0].Value" }.Order());
        result.Errors.Values.SelectMany(v => v).ShouldAllBe(message => message == "Value is invalid.");
    }

    [Fact]
    public async Task WithBadInput()
    {
        var (rsp, res) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 10,
                    Name = "x",
                    Meta = new()
                    {
                        Age = 0,
                        Gender = ""
                    },
                    Children = new()
                    {
                        new()
                        {
                            Name = "c",
                            Age = 101,
                            Gender = ""
                        }
                    }
                });

        List<string> expected = ["id", "name", "children[0].Name", "children[0].Age", "children[0].Gender", "meta.Gender", "meta.Age"];
        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.Count.ShouldBe(7);
        res.Errors.Keys.Order().ShouldBe(expected.Order());
    }

    [Fact]
    public async Task WithOkInput()
    {
        var (resp, _) =
            await App.Client.POSTAsync<Endpoint, Request, ErrorResponse>(
                new()
                {
                    Id = 100,
                    Name = "pass",
                    Meta = new()
                    {
                        Age = 38,
                        Gender = "Male"
                    },
                    Children = new()
                    {
                        new()
                        {
                            Name = "child1",
                            Age = 3,
                            Gender = "Female"
                        },
                        new()
                        {
                            Name = "child2",
                            Age = 0,
                            Gender = "Male"
                        }
                    }
                });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
