using Microsoft.AspNetCore.Hosting;
using NativeAotChecker.Endpoints.Events;

namespace NativeAotCheckerTests;

// Native fixtures cache by TProgram, so use a distinct marker from the same entry-point assembly.
public class InheritanceApp : AppFixture<InheritanceProgram>
{
    public InheritanceApp()
    {
        var options = new Config().Serializer.Options;
        if (options.PropertyNamingPolicy != System.Text.Json.JsonNamingPolicy.SnakeCaseLower)
            options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    }

    protected override void ConfigureApp(IWebHostBuilder app)
        => app.UseSetting("Testing:EventInheritance", "true");

    protected override ValueTask ConfigureAotTargetAsync(AotTargetOptions options)
    {
#if NET11_0_OR_GREATER
        const string framework = "net11.0";
#else
        const string framework = "net10.0";
#endif
        options.AdditionalPublishArguments = $"-f {framework}";
        options.AotBuildOutputPath = Path.Combine(AppContext.BaseDirectory, "aot");
        options.BuildTimeoutMinutes = 15;
        options.ReadyTimeoutSeconds = 60;
        options.EnvironmentVariables["Testing__EventInheritance"] = "true";
        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition("EventInheritance", DisableParallelization = true)]
public sealed class EventInheritanceCollection;

[Collection("EventInheritance")]
public class EventInheritanceTests(InheritanceApp app) : TestBase<InheritanceApp>
{
    [Theory]
    [InlineData(false, false, 1, 1, 1)]
    [InlineData(false, true, 1, 1, 0)]
    [InlineData(true, false, 0, 0, 1)]
    public async Task Inherited_Event_Publish(bool interfaceOnly, bool filtered, int baseCalls, int derivedCalls, int interfaceCalls)
    {
        var (response, result, error) = await app.Client.POSTAsync<EventInheritanceEndpoint, InheritanceRequest, InheritanceResponse>(
                                           new() { InterfaceOnly = interfaceOnly, Filtered = filtered });
        response.IsSuccessStatusCode.ShouldBeTrue(error);
        result.BaseCalls.ShouldBe(baseCalls);
        result.DerivedCalls.ShouldBe(derivedCalls);
        result.InterfaceCalls.ShouldBe(interfaceCalls);
    }
}
