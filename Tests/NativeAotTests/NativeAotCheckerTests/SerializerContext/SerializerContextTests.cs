using NativeAotChecker.Endpoints.SerializerCtxGen;

namespace NativeAotCheckerTests;

public class SerializerContextTests(App app)
{
    [Fact]
    public async Task Collection_Dto_Serialization()
    {
        var req = new[]
        {
            new Request { FirstName = "John", LastName = "Doe" },
            new Request { FirstName = "Jane", LastName = "Smith" }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<EndpointWithArrayResponse, Request[], IEnumerable<Response>>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Count().ShouldBe(2);
        res.First().FullName.ShouldBe("John Doe");
        res.Last().FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task Fluent_Req_NoRes_Endpoint()
    {
        var req = new FluentNoResRequest { FirstName = "John", LastName = "Doe" };

        var (rsp, res) = await app.Client.POSTAsync<FluentNoResEndpoint, FluentNoResRequest, string>(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBe("John Doe");
    }

    [Fact]
    public async Task Fluent_Req_Res_Endpoint()
    {
        var req = new FluentResRequest { FirstName = "Jane", LastName = "Smith" };

        var (rsp, res, err) = await app.Client.POSTAsync<FluentResEndpoint, FluentResRequest, FluentResResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task Fluent_NoReq_NoRes_Endpoint()
    {
        var (rsp, res, err) = await app.Client.GETAsync<FluentNoReqNoResEndpoint, string>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBe("Hello from NoReq.NoRes");
    }

    [Fact]
    public async Task Fluent_NoReq_Res_Endpoint()
    {
        var (rsp, res, err) = await app.Client.GETAsync<FluentNoReqResEndpoint, FluentNoReqResResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Message.ShouldBe("Hello from NoReq.Res");
        res.Timestamp.ShouldBeGreaterThan(default);
    }

    [Fact]
    public async Task Fluent_Req_Res_Map_Endpoint()
    {
        var req = new FluentMapRequest { FirstName = "John", LastName = "Doe", Age = 30 };

        var (rsp, res, err) = await app.Client.POSTAsync<FluentResMapEndpoint, FluentMapRequest, FluentMapResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FullName.ShouldBe("John Doe");
        res.Age.ShouldBe(30);
        res.MapperWasUsed.ShouldBeTrue();
    }

    [Fact]
    public async Task Fluent_Req_NoRes_Map_Endpoint()
    {
        var req = new FluentNoResMapRequest { FirstName = "Jane", LastName = "Smith", Age = 25 };

        var (rsp, res) = await app.Client.POSTAsync<FluentNoResMapEndpoint, FluentNoResMapRequest, string>(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBe("Created: Jane Smith (age 25)");
    }

    [Fact]
    public async Task Fluent_NoReq_Res_Map_Endpoint()
    {
        var (rsp, res, err) = await app.Client.GETAsync<FluentNoReqResMapEndpoint, FluentNoReqResMapResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FullName.ShouldBe("John Doe");
        res.Age.ShouldBe(30);
        res.MapperWasUsed.ShouldBeTrue();
    }
}