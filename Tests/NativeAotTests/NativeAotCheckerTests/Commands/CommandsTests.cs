using NativeAotChecker.Endpoints.Commands;

namespace NativeAotCheckerTests;

public class CommandsTests(App app)
{
    [Fact]
    public async Task Command_Execution_With_Result()
    {
        var (rsp, res, err) = await app.Client.GETAsync<CommandExecutionEndpoint, CommandExecutionRequest, string>(new() { Name = "IRIS" });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe("SIRI");
        else
            Assert.Fail(err);
    }

    [Fact]
    public async Task Command_Middleware_Executes_In_Correct_Order()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<CommandMiddlewareEndpoint, CommandMiddlewareRequest, CommandMiddlewareResponse>(
                                  new()
                                  {
                                      Input = "test"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Result.ShouldBe("[ first-in >> second-in >> third-in >> [handler] << third-out << second-out << first-out ]");
    }
}
