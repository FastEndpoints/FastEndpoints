using System.Net;

namespace Processors;

public class ProcessorTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task PreProcessorShortCircuitingWhileValidatorFails()
    {
        var x = await App.Client.GETAsync<
                    TestCases.PreProcessorShortWhileValidatorFails.Endpoint,
                    TestCases.PreProcessorShortWhileValidatorFails.Request,
                    object>(
                    new()
                    {
                        Id = 0
                    });

        x.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        x.Result.ToString().ShouldBe("hello from pre-processor!");
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

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.ShouldNotBeNull();
        res.Errors.Count.ShouldBe(2);
        res.Errors["x"].First().ShouldBe("blah");
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
        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Count.ShouldBe(5);
        res.ShouldBe(["zero", "one", "two", "three", "four"]);
    }

    [Fact]
    public async Task GenericGlobalProcessors()
    {
        var req = new TestCases.GlobalGenericProcessorTest.Request
        {
            PreProcRan = false,
            PostProcRan = false
        };

        var (rsp, res) = await App.Client.POSTAsync<
                             TestCases.GlobalGenericProcessorTest.Endpoint,
                             TestCases.GlobalGenericProcessorTest.Request,
                             TestCases.GlobalGenericProcessorTest.Request>(req);
        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.PreProcRan.ShouldBeTrue();
        res.PostProcRan.ShouldBeTrue();
    }

    [Fact]
    public async Task PreProcessorShortCircuitMissingHeader()
    {
        var (rsp, res) = await App.Client.GETAsync<
                             Sales.Orders.Retrieve.Endpoint,
                             Sales.Orders.Retrieve.Request,
                             ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors.ShouldNotBeNull();
        res.Errors.Count.ShouldBe(1);
        res.Errors.ShouldContainKey("missingHeaders");
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

        rsp.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task PreProcessorShortCircuitHandlerExecuted()
    {
        var (rsp, res) = await App.CustomerClient.GETAsync<
                             Sales.Orders.Retrieve.Endpoint,
                             Sales.Orders.Retrieve.Request,
                             ErrorResponse>(new() { OrderID = "order1" });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Message.ShouldBe("ok!");
    }

    [Fact]
    public async Task ProcessorStateWorks()
    {
        var x = await App.Client.GETAsync<
                    TestCases.ProcessorStateTest.Endpoint,
                    TestCases.ProcessorStateTest.Request,
                    string>(new() { Id = 10101 });

        x.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        x.Result.ShouldBe("10101 jane doe True");
    }

    [Fact]
    public async Task PostProcessorCanHandleExceptions()
    {
        var x = await App.Client.GETAsync<
                    TestCases.PostProcessorTest.Endpoint,
                    TestCases.PostProcessorTest.Request,
                    TestCases.PostProcessorTest.ExceptionDetailsResponse>(new() { Id = 10101 });

        x.Response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        x.Result.Type.ShouldBe(nameof(NotImplementedException));
    }

    [Fact]
    public async Task ExceptionIsThrownWhenAPostProcDoesntHandleExceptions()
    {
        var (rsp, res) = await App.Client.GETAsync<TestCases.PostProcessorTest.EpNoPostProcessor, InternalErrorResponse>();
        rsp.IsSuccessStatusCode.ShouldBeFalse();
        res.Code.ShouldBe(500);
        res.Reason.ShouldBe("The method or operation is not implemented.");
    }
}