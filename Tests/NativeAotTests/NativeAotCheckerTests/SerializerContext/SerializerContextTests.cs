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
}