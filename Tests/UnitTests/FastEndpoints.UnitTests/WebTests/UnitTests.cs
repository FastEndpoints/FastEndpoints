using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Web.Services;
using Xunit;

namespace FastEndpoints.UnitTests.WebTests;

public class UnitTests
{
    [Fact]
    public async Task handle_with_correct_input_without_context_should_set_create_customer_response_correctly()
    {
        var emailer = A.Fake<IEmailService>();
        A.CallTo(() => emailer.SendEmail()).Returns("test email");

        var ep = Factory.Create<Customers.Create.Endpoint>(ctx =>
            {
                var services = new ServiceCollection();

                var logger = A.Fake<ILogger<Endpoint<Customers.Create.Request, object>>>();
                services.AddSingleton(logger);

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
}