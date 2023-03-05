using FakeItEasy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Web.Services;
using Xunit;

namespace FastEndpoints.UnitTests.WebTests;

public class UnitTests
{
    [Fact]
    public async Task endpoint_with_mapper_returns_correct_response()
    {
        //arrange
        var logger = A.Fake<ILogger<TestCases.MapperTest.Endpoint>>();
        var ep = Factory.Create<TestCases.MapperTest.Endpoint>(logger);
        ep.Map = new TestCases.MapperTest.Mapper();
        var req = new TestCases.MapperTest.Request
        {
            FirstName = "john",
            LastName = "doe",
            Age = 22
        };

        //act
        await ep.HandleAsync(req, default);

        //assert
        ep.Response.Should().NotBeNull();
        ep.Response.Name.Should().Be("john doe");
        ep.Response.Age.Should().Be(22);
    }

    [Fact]
    public async Task handle_with_correct_input_without_context_should_set_create_customer_response_correctly()
    {
        var emailer = A.Fake<IEmailService>();
        A.CallTo(() => emailer.SendEmail()).Returns("test email");

        var ep = Factory.Create<Customers.Create.Endpoint>(ctx =>
            {
                var services = new ServiceCollection();

                var loggerFactory = A.Fake<ILoggerFactory>();
                services.AddSingleton(loggerFactory);

                ctx.RequestServices = services.BuildServiceProvider();
            }
            , emailer);

        var req = new Customers.Create.Request
        {
            CreatedBy = "by harry potter",
        };

        await ep.HandleAsync(req, default);

        ep.Response.Should().Be("test email by harry potter");
    }

    [Fact]
    public async Task handle_with_correct_input_with_context_should_set_login_admin_response_correctly()
    {
        //arrange
        var fakeConfig = A.Fake<IConfiguration>();
        A.CallTo(() => fakeConfig["TokenKey"]).Returns("0000000000000000");

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
        ep.ValidationFailed.Should().BeFalse();
        rsp.Should().NotBeNull();
        rsp.Permissions.Should().Contain("Inventory_Delete_Item");
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
        ep.ValidationFailed.Should().BeTrue();
        ep.ValidationFailures.Any(f => f.ErrorMessage == "Authentication Failed!").Should().BeTrue();
    }

    [Fact]
    public async Task execute_customer_recent_list_should_return_correct_data()
    {
        var endpoint = Factory.Create<Customers.List.Recent.Endpoint>();
        var res = await endpoint.ExecuteAsync(default) as Customers.List.Recent.Response;

        res?.Customers?.Count().Should().Be(3);
        res?.Customers?.First().Key.Should().Be("ryan gunner");
        res?.Customers?.Last().Key.Should().Be(res?.Customers?.Last().Key);
    }

    [Fact]
    public async Task created_at_success()
    {
        var linkgen = A.Fake<LinkGenerator>();

        var ep = Factory.Create<Inventory.Manage.Create.Endpoint>(ctx =>
        {
            var services = new ServiceCollection();
            services.AddSingleton(linkgen);
            ctx.RequestServices = services.BuildServiceProvider();
        });

        await ep.HandleAsync(new()
        {
            Name = "Grape Juice",
            Description = "description",
            ModifiedBy = "me",
            Price = 100,
            GenerateFullUrl = false
        }, default);
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
        ep.Response.Should().Be("101 blah");
        state.Duration.Should().BeGreaterThan(250);
    }
}