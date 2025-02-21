using System.Text.Json;
using FakeItEasy;
using FastEndpoints;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestCases.TypedResultTest;
using Web.Services;
using Xunit;

namespace Web;

public class WebTests
{
    [Fact]
    public async Task mapper_endpoint_setting_mapper_manually()
    {
        //arrange
        var logger = A.Fake<ILogger<TestCases.MapperTest.Endpoint>>();
        var ep = Factory.Create<TestCases.MapperTest.Endpoint>(logger);
        ep.Map = new();
        var req = new TestCases.MapperTest.Request
        {
            FirstName = "john",
            LastName = "doe",
            Age = 22
        };

        //act
        await ep.HandleAsync(req, default);

        //assert
        ep.Response.ShouldNotBeNull();
        ep.Response.Name.ShouldBe("john doe");
        ep.Response.Age.ShouldBe(22);
    }

    [Fact]
    public async Task mapper_endpoint_resolves_mapper_automatically()
    {
        //arrange
        var logger = A.Fake<ILogger<TestCases.MapperTest.Endpoint>>();
        var ep = Factory.Create<TestCases.MapperTest.Endpoint>(logger);
        var req = new TestCases.MapperTest.Request
        {
            FirstName = "john",
            LastName = "doe",
            Age = 22
        };

        //act
        await ep.HandleAsync(req, default);

        //assert
        ep.Response.ShouldNotBeNull();
        ep.Response.Name.ShouldBe("john doe");
        ep.Response.Age.ShouldBe(22);
    }

    [Fact]
    public async Task endpoint_with_mapper_throws_mapper_not_set()
    {
        var logger = A.Fake<ILogger<TestCases.MapperTest.Endpoint>>();
        var ep = Factory.Create<TestCases.MapperTest.Endpoint>(logger);

        ep.Map = null!;
        ep.Definition.MapperType = null;

        var req = new TestCases.MapperTest.Request
        {
            FirstName = "john",
            LastName = "doe",
            Age = 22
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => ep.HandleAsync(req, CancellationToken.None));
        ex.Message.ShouldBe("Endpoint mapper is not set!");
    }

    [Fact]
    public async Task handle_with_correct_input_without_context_should_set_create_customer_response_correctly()
    {
        var emailer = A.Fake<IEmailService>();
        A.CallTo(() => emailer.SendEmail()).Returns("test email");

        var ep = Factory.Create<Customers.Create.Endpoint>(emailer);

        var req = new Customers.Create.Request
        {
            CreatedBy = "by harry potter"
        };

        await ep.HandleAsync(req, default);

        ep.Response.ShouldBe("test email by harry potter");
    }

    [Fact]
    public async Task handle_with_correct_input_with_property_di_without_context_should_set_create_customer_response_correctly()
    {
        var emailer = A.Fake<IEmailService>();
        A.CallTo(() => emailer.SendEmail()).Returns("test email");

        var ep = Factory.Create<Customers.CreateWithPropertiesDI.Endpoint>(
            ctx =>
            {
                ctx.AddTestServices(s => s.AddSingleton(emailer));
            });

        var req = new Customers.CreateWithPropertiesDI.Request
        {
            CreatedBy = "by harry potter"
        };

        await ep.HandleAsync(req, default);

        ep.Response.ShouldBe("test email by harry potter");
    }

    [Fact]
    public async Task handle_with_correct_input_with_context_should_set_login_admin_response_correctly()
    {
        //arrange
        var fakeConfig = A.Fake<IConfiguration>();
        A.CallTo(() => fakeConfig["TokenKey"]).Returns("00000000000000000000000000000000");

        var ep = Factory.Create<Admin.Login.Endpoint>(
            A.Fake<ILogger<Admin.Login.Endpoint>>(),
            A.Fake<IEmailService>(),
            fakeConfig);

        var req = new Admin.Login.Request
        {
            UserName = "admin",
            Password = "pass"
        };

        //act
        await ep.HandleAsync(req, default);
        var rsp = ep.Response;

        //assert
        ep.ValidationFailed.ShouldBeFalse();
        rsp.ShouldNotBeNull();
        rsp.Permissions.ShouldContain("Inventory_Delete_Item");
    }

    [Fact]
    public async Task handle_with_bad_input_should_set_admin_login_validation_failed()
    {
        //arrange
        var ep = Factory.Create<Admin.Login.Endpoint>(
            A.Fake<ILogger<Admin.Login.Endpoint>>(),
            A.Fake<IEmailService>(),
            A.Fake<IConfiguration>());

        var req = new Admin.Login.Request
        {
            UserName = "x",
            Password = "y"
        };

        //act
        await ep.HandleAsync(req, default);

        //assert
        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Any(f => f.ErrorMessage == "Authentication Failed!").ShouldBeTrue();
    }

    [Fact]
    public async Task execute_customer_recent_list_should_return_correct_data()
    {
        var endpoint = Factory.Create<Customers.List.Recent.Endpoint>();
        var res = await endpoint.ExecuteAsync(default) as Customers.List.Recent.Response;

        res?.Customers?.Count().ShouldBe(3);
        res?.Customers?.First().Key.ShouldBe("ryan gunner");
        res?.Customers?.Last().Key.ShouldBe(res?.Customers?.Last().Key);
    }

    [Fact]
    public async Task union_type_result_returning_endpoint()
    {
        var ep = Factory.Create<MultiResultEndpoint>();

        var res0 = await ep.ExecuteAsync(new() { Id = 0 }, CancellationToken.None);
        res0.Result.ShouldBeOfType<NotFound>();

        var res1 = await ep.ExecuteAsync(new() { Id = 1 }, CancellationToken.None);
        var errors = (res1.Result as ProblemDetails)!.Errors;
        errors.Count().ShouldBe(1);
        errors.Single(e => e.Name == nameof(Request.Id)).Reason.ShouldBe("value has to be greater than 1");

        var res2 = await ep.ExecuteAsync(new() { Id = 2 }, CancellationToken.None);
        var response = res2.Result as Ok<Response>;
        response!.StatusCode.ShouldBe(200);
        response.Value!.RequestId.ShouldBe(2);
    }

    [Fact]
    public async Task created_at_success()
    {
        var linkgen = A.Fake<LinkGenerator>();

        var ep = Factory.Create<Inventory.Manage.Create.Endpoint>(
            ctx =>
            {
                ctx.AddTestServices(s => s.AddSingleton(linkgen));
            });

        await ep.HandleAsync(
            new()
            {
                Name = "Grape Juice",
                Description = "description",
                ModifiedBy = "me",
                Price = 100,
                GenerateFullUrl = false
            },
            default);

        ep.HttpContext.Response.Headers.ContainsKey("Location");
        ep.HttpContext.Response.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task processor_state_access_from_unit_test()
    {
        //arrange
        var ep = Factory.Create<TestCases.ProcessorStateTest.Endpoint>();

        var state = ep.ProcessorState<TestCases.ProcessorStateTest.Thingy>();
        state.Id = 101;
        state.Name = "blah";

        //act
        await ep.HandleAsync(new() { Id = 0 }, default);

        //assert
        // False represents the lack of global state addition from endpoint without global preprocessor
        ep.Response.ShouldBe("101 blah False");
        state.Duration.ShouldBeGreaterThan(95);
    }

    [Fact]
    public async Task unit_test_concurrency_and_httpContext_isolation()
    {
        await Parallel.ForEachAsync(
            Enumerable.Range(1, 100),
            async (id, _) =>
            {
                var ep = Factory.Create<TestCases.UnitTestConcurrencyTest.Endpoint>(
                    ctx =>
                    {
                        ctx.AddTestServices(s => s.AddSingleton(new TestCases.UnitTestConcurrencyTest.SingltonSVC(id)));
                    });

                (await ep.ExecuteAsync(new() { Id = id }, default)).ShouldBe(id);
            });
    }

    [Fact]
    public async Task list_element_validation_error()
    {
        var ep = Factory.Create<TestCases.ValidationErrorTest.ListValidationErrorTestEndpoint>();
        await ep.HandleAsync(
            new()
            {
                NumbersList = [1, 2, 3]
            },
            default);

        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Count.ShouldBe(3);
        ep.ValidationFailures[0].PropertyName.ShouldBe("NumbersList[0]");
        ep.ValidationFailures[1].PropertyName.ShouldBe("NumbersList[1]");
        ep.ValidationFailures[2].PropertyName.ShouldBe("NumbersList[2]");
    }

    [Fact]
    public async Task dict_element_validation_error()
    {
        var ep = Factory.Create<TestCases.ValidationErrorTest.DictionaryValidationErrorTestEndpoint>();
        await ep.HandleAsync(
            new()
            {
                StringDictionary = new()
                {
                    { "a", "1" },
                    { "b", "2" }
                }
            },
            default);

        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Count.ShouldBe(2);
        ep.ValidationFailures[0].PropertyName.ShouldBe("StringDictionary[\"a\"]");
        ep.ValidationFailures[1].PropertyName.ShouldBe("StringDictionary[\"b\"]");
    }

    [Fact]
    public async Task array_element_validation_error()
    {
        var ep = Factory.Create<TestCases.ValidationErrorTest.ArrayValidationErrorTestEndpoint>();
        await ep.HandleAsync(
            new()
            {
                StringArray =
                [
                    "a",
                    "b"
                ]
            },
            default);

        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Count.ShouldBe(2);
        ep.ValidationFailures[0].PropertyName.ShouldBe("StringArray[0]");
        ep.ValidationFailures[1].PropertyName.ShouldBe("StringArray[1]");
    }

    [Fact]
    public async Task array_element_object_property_validation_error()
    {
        var ep = Factory.Create<TestCases.ValidationErrorTest.ObjectArrayValidationErrorTestEndpoint>();
        await ep.HandleAsync(
            new()
            {
                ObjectArray =
                [
                    new() { Test = "a" },
                    new() { Test = "b" }
                ]
            },
            default);

        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Count.ShouldBe(2);
        ep.ValidationFailures[0].PropertyName.ShouldBe("ObjectArray[0].Test");
        ep.ValidationFailures[1].PropertyName.ShouldBe("ObjectArray[1].Test");
    }

    [Fact]
    public async Task list_in_list_validation_error()
    {
        var ep = Factory.Create<TestCases.ValidationErrorTest.ListInListValidationErrorTestEndpoint>();
        await ep.HandleAsync(
            new()
            {
                NumbersList =
                [
                    new() { 1, 2 },
                    new() { 3, 4 }
                ]
            },
            default);

        ep.ValidationFailed.ShouldBeTrue();
        ep.ValidationFailures.Count.ShouldBe(4);
        ep.ValidationFailures[0].PropertyName.ShouldBe("NumbersList[0][0]");
        ep.ValidationFailures[1].PropertyName.ShouldBe("NumbersList[0][1]");
        ep.ValidationFailures[2].PropertyName.ShouldBe("NumbersList[1][0]");
        ep.ValidationFailures[3].PropertyName.ShouldBe("NumbersList[1][1]");
    }

    [Fact]
    public async Task problem_details_serialization_test()
    {
        var problemDetails = new ProblemDetails(
            new List<ValidationFailure>
            {
                new("p1", "v1"),
                new("p2", "v2")
            },
            "instance",
            "trace",
            400);

        var json = JsonSerializer.Serialize(problemDetails);
        var res = JsonSerializer.Deserialize<ProblemDetails>(json)!;
        res.Errors = new HashSet<ProblemDetails.Error>(res.Errors);
        res.ShouldBeEquivalentTo(problemDetails);
    }
}