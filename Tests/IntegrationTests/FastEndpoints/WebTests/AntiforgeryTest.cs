using FastEndpoints;
using Microsoft.AspNetCore.Antiforgery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestClass = TestCases.AntiforgeryTest;

namespace Int.FastEndpoints.WebTests;




public class AntiforgeryTest : TestClass<Fixture>
{
    public AntiforgeryTest(Fixture f, ITestOutputHelper o) : base(f, o) { }


    [Fact]
    public async Task WithNoNeed()
    {
        var (rsp, res) = await Fixture.GuestClient.GETAsync<TestClass.AntUiEndpoint, ErrorResponse>();
        var content = await rsp.Content.ReadAsStringAsync();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task WithBadToken()
    {
        var sendData = new MultipartFormDataContent
        {
            { new StringContent("qweryuiopasdfghjklzxcvbnm"), "__RequestVerificationToken" },
        };

        //RequestUri = new($"{client.BaseAddress}{requestUri.TrimStart('/')}")
        var rsp = await Fixture.GuestClient.SendAsync(new HttpRequestMessage
        {
            Content = sendData,
            RequestUri = new Uri($"{Fixture.GuestClient.BaseAddress}api/{TestClass.Endpoints.Post}"),
            Method = HttpMethod.Post
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = rsp.Content.ReadAsStringAsync();
        content.Result.Should().Contain("Invalid");
    }

    [Fact]
    public async Task WithOkToken()
    {
        //得到token
        var (rsp1, res1) = await Fixture.GuestClient.GETAsync<TestClass.AntTokenEndpoint, TestClass.Token>();

        var sendData = new MultipartFormDataContent
        {
            { new StringContent(res1.value!), "__RequestVerificationToken" },
        };

        //Post
        var rsp = await Fixture.GuestClient.SendAsync(new HttpRequestMessage
        {
            Content = sendData,
            RequestUri = new Uri($"{Fixture.GuestClient.BaseAddress}api/{TestClass.Endpoints.Post}"),
            Method = HttpMethod.Post
        });

        var content =await rsp.Content.ReadAsStringAsync();

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("antiforgery success");
    }
}