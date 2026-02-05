using NativeAotChecker.Endpoints.Processors;

namespace NativeAotCheckerTests;

public class ProcessorsTests(App app)
{
    [Fact]
    public async Task Pre_Processors_Execute()
    {
        var id = Guid.NewGuid().ToString();
        var (rsp, res, err) = await app.Client.POSTAsync<PreProcessorEndpoint, PreProcessorRequest, PreProcessorResponse>(
                                  new()
                                  {
                                      InputValue = id
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ResultValue.ShouldBe($"PROCESSED:{id}");
        res.PreProcessorExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task Open_Generic_Global_PreProcessor_Executes()
    {
        var id = Guid.NewGuid().ToString();
        var (rsp, res, err) = await app.Client.POSTAsync<
                                  OpenGenericGlobalProcessorEndpoint,
                                  OpenGenericGlobalProcessorRequest,
                                  OpenGenericGlobalProcessorResponse>(new() { InputValue = id });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ResultValue.ShouldBe($"PROCESSED:{id}");
        res.GlobalPreProcessorExecuted.ShouldBeTrue();
    }
}
