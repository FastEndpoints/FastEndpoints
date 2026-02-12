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

    [Fact]
    public async Task Generic_Command_Handler_Registration()
    {
        var req = new GenericCommandRequest
        {
            OperationType = "Create",
            ProductName = "Test Product",
            ProductPrice = 99.99m
        };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericCommandEndpoint, GenericCommandRequest, GenericCommandResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Success.ShouldBeTrue();
        res.OperationType.ShouldBe("Create");
        res.ProductName.ShouldBe("Test Product");
        res.ProductPrice.ShouldBe(99.99m);
        res.HandlerType.ShouldContain("AotGenericCommandHandler");
    }
}