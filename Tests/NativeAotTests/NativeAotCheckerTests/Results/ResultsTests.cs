using NativeAotChecker.Endpoints.Results;

namespace NativeAotCheckerTests;

public class ResultsTests(App app)
{
    [Fact]
    public async Task IResult_Returning_Endpoint()
    {
        var (rsp, res, err) = await app.Client.GETAsync<ResultReturningEndpoint, string>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ShouldBe("hello");
    }
}
