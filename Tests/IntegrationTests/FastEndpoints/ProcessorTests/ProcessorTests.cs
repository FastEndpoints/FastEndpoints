using System.Net;

namespace Processors;

public class ProcessorTests(AppFixture App) : TestBase<AppFixture>
{
    [Fact]
    public async Task PreProcessorShortCircuitingWhileValidatorFails()
    {
        var x = await App.Client.GETAsync<
                    TestCases.PrecessorShortWhileValidatorFails.Endpoint,
                    TestCases.PrecessorShortWhileValidatorFails.Request,
                    object>(
                    new()
                    {
                        Id = 0
                    });

        x.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        x.Result.ToString().Should().Be("hello from pre-processor!");
    }

    [Fact]
    public async Task PreProcessorsAreRunIfValidationFailuresOccur()
    {
        var (rsp, res) = await App.AdminClient.POSTAsync<
                             TestCases.PreProcessorIsRunOnValidationFailure.Endpoint,
                             TestCases.PreProcessorIsRunOnValidationFailure.Request,
                             ErrorResponse>(
                             new()
                             {
                                 FailureCount = 0,
                                 FirstName = ""
                             });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().NotBeNull();
        res.Errors.Count.Should().Be(2);
        res.Errors["x"].First().Should().Be("blah");
    }

    [Fact]
    public async Task ProcessorAttributes()
    {
        var (rsp, res) =
            await App.Client.POSTAsync<
                TestCases.ProcessorAttributesTest.Endpoint,
                TestCases.ProcessorAttributesTest.Request,
                List<string>>(
                new()
                {
                    Values = ["zero"]
                });
        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.Count.Should().Be(5);
        res.Should().Equal(["zero", "one", "two", "three", "four"]);
    }

    [Fact]
    public async Task PreProcessorShortCircuitMissingHeader()
    {
        var (rsp, res) = await App.Client.GETAsync<
                             Sales.Orders.Retrieve.Endpoint,
                             Sales.Orders.Retrieve.Request,
                             ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().NotBeNull();
        res.Errors.Count.Should().Be(1);
        res.Errors.Should().ContainKey("MissingHeaders");
    }

    [Fact]
    public async Task PreProcessorShortCircuitWrongHeaderValue()
    {
        var (rsp, _) = await App.AdminClient.POSTAsync<
                           Sales.Orders.Retrieve.Endpoint,
                           Sales.Orders.Retrieve.Request,
                           object>(
                           new()
                           {
                               OrderID = "order1"
                           });

        rsp.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task PreProcessorShortCircuitHandlerExecuted()
    {
        var (rsp, res) = await App.CustomerClient.GETAsync<
                             Sales.Orders.Retrieve.Endpoint,
                             Sales.Orders.Retrieve.Request,
                             ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Message.Should().Be("ok!");
    }

    [Fact]
    public async Task ProcessorStateWorks()
    {
        var x = await App.Client.GETAsync<
                    TestCases.ProcessorStateTest.Endpoint,
                    TestCases.ProcessorStateTest.Request,
                    string>(new() { Id = 10101 });

        x.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        x.Result.Should().Be("10101 jane doe True");
    }

    [Fact]
    public async Task PostProcessorCanHandleExceptions()
    {
        var x = await App.Client.GETAsync<
                    TestCases.PostProcessorTest.Endpoint,
                    TestCases.PostProcessorTest.Request,
                    TestCases.PostProcessorTest.ExceptionDetailsResponse>(new() { Id = 10101 });

        x.Response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        x.Result.Type.Should().Be(nameof(NotImplementedException));
    }

    [Fact]
    public async Task ExceptionIsThrownWhenAPostProcDoesntHandleExceptions()
    {
        var (rsp, res) = await App.Client.GETAsync<TestCases.PostProcessorTest.EpNoPostProcessor, InternalErrorResponse>();
        rsp.IsSuccessStatusCode.Should().BeFalse();
        res.Code.Should().Be(500);
        res.Reason.Should().Be("The method or operation is not implemented.");
    }
}