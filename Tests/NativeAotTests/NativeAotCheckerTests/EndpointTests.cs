using System.Net;
using NativeAotChecker.Endpoints;

namespace NativeAotCheckerTests;

public class EndpointTests(App app)
{
    [Fact]
    public async Task Json_Serialization_With_Http_Post()
    {
        var (rsp, res) = await app.Client.POSTAsync<JsonPostEndpoint, JsonPostRequest, JsonPostResponse>(
                             new()
                             {
                                 FirstName = "Jane",
                                 LastName = "Doe"
                             });

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Message.ShouldBe("Hello Jane Doe!");
    }

    [Fact]
    public async Task Non_Json_Binding()
    {
        var id = Guid.NewGuid();
        var req = new NonJsonRequest
        {
            Id = id.ToString(),
            Identifier = id,
            UserName = new()
            {
                Value = id.ToString()
            }
        };
        var (rsp, res) = await app.Client.GETAsync<NonJsonBindingEndpoint, NonJsonRequest, NonJsonResponse>(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeEquivalentTo(
            new NonJsonResponse
            {
                Id = id.ToString(),
                Identifier = req.Identifier,
                UserName = req.UserName.Value
            });
    }

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
    public async Task Event_Publish()
    {
        var id = Guid.NewGuid();
        var (rsp, res, err) = await app.Client.POSTAsync<EventPublishEndpoint, EventPublishRequest, Guid>(new() { Id = id });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe(id);
        else
            Assert.Fail(err);
    }

    [Fact]
    public async Task Queue_Jobs_That_Return_Results()
    {
        var ids = new List<(Guid id, Task<TestResult<string>> responseTask)>();

        for (var i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            var task = app.Client.GETAsync<JobQueueEndpoint, JobQueueRequest, string>(new() { Id = id });
            ids.Add((id, task));
        }

        var results = await Task.WhenAll(ids.Select(i => i.responseTask));

        for (var i = 0; i < 10; i++)
        {
            var (id, _) = ids[i];
            var (rsp, res, err) = results[i];

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.ShouldBe(id.ToString());
        }
    }

    [Fact]
    public async Task Multi_Source_Binding()
    {
        var (_, token) = await app.Client.GETAsync<GetJwtTokenEndpoint, string>();

        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var req = new MultiSourceBindingRequest
        {
            Description = "Test description from JSON",
            Id = 456,
            Category = "test-category",
            FormValue = "Hello from form",
            RequestId = "test-req-789"
        };

        var (rsp, res, err) =
            await app.Client.POSTAsync<MultiSourceBindingEndpoint, MultiSourceBindingRequest, MultiSourceBindingResponse>(req, sendAsFormData: true);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Description.ShouldBe("Test description from JSON");
        res.Id.ShouldBe(456);
        res.Category.ShouldBe("test-category");
        res.UserId.ShouldBe("001");
        res.FormValue.ShouldBe("Hello from form");
        res.RequestId.ShouldBe("test-req-789");
    }

    [Fact]
    public async Task Complex_Object_Query_Binding()
    {
        var id = Guid.NewGuid();
        var req = new ComplexObjectQueryBindingRequest
        {
            Person = new()
            {
                Id = id,
                Name = "John Doe",
                Age = 30
            },
            Category = "test-category"
        };

        var (rsp, res, err) = await app.Client.GETAsync<ComplexObjectQueryBindingEndpoint, ComplexObjectQueryBindingRequest, ComplexObjectQueryBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Person.Id.ShouldBe(id);
        res.Person.Name.ShouldBe("John Doe");
        res.Person.Age.ShouldBe(30);
        res.Category.ShouldBe("test-category");
    }

    [Fact]
    public async Task Result_Returning_Endpoint()
    {
        var (rsp, res, err) = await app.Client.GETAsync<ResultReturningEndpoint, string>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ShouldBe("hello");
    }

    [Fact]
    public async Task Custom_Endpoint_Level_Binder()
    {
        var id = Guid.NewGuid().ToString();
        app.Client.DefaultRequestHeaders.Add("X-Custom-Value", id);

        var (rsp, res, err) = await app.Client.POSTAsync<CustomBinderEndpoint, CustomBinderRequest, CustomBinderResponse>(
                                  new()
                                  {
                                      InputValue = "ignored",
                                      ProcessedValue = "ignored"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.InputValue.ShouldBe(id);
        res.ProcessedValue.ShouldBe($"CUSTOM-BINDER:{id}");
        res.BinderWasUsed.ShouldBeTrue();
    }

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
    public async Task Error_Response_With_Property_Expression_Use()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<ErrorWithPropertyExpressionEndpoint, ErrorWithPropertyExpressionRequest, ErrorResponse>(
                                  new()
                                  {
                                      Items = ["123", "321"]
                                  });

        if (rsp.StatusCode != HttpStatusCode.BadRequest)
            Assert.Fail(err);

        res.Errors.Count.ShouldBe(1);
        res.Errors.Keys.ShouldContain("items[1]");
    }
}