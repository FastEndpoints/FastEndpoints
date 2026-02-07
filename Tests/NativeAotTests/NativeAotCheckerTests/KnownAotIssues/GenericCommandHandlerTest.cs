using System.Net.Http.Json;
using NativeAotChecker.Endpoints.KnownAotIssues;

namespace NativeAotCheckerTests;

public class GenericCommandHandlerTest(App app)
{
    /// <summary>
    /// Tests generic command handlers ICommandHandler<TCommand, TResult> in AOT mode.
    /// AOT ISSUE: Generic command handlers require source generator to pre-generate handler bindings.
    /// Without AOT-specific code gen, the runtime cannot resolve ICommandHandler<,> implementations.
    /// MakeGenericType() calls fail as generic type instantiation is not available at runtime.
    /// </summary>
    [Fact] // AOT ISSUE: Generic command handlers require source generator changes for AOT support
    public async Task Generic_Command_Handler_Works_In_AOT_Mode()
    {
        var req = new GenericCommandRequest
        {
            OperationType = "Create",
            ProductName = "Test Product",
            ProductPrice = 99.99m
        };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericCommandEndpoint,
GenericCommandRequest, GenericCommandResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Success.ShouldBeTrue();
        res.OperationType.ShouldBe("Create");
        res.ProductName.ShouldBe("Test Product");
        res.ProductPrice.ShouldBe(99.99m);
        res.HandlerType.ShouldContain("AotGenericCommandHandler");
    }
}
