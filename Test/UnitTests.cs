using FakeItEasy;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Web.Services;

namespace Test;

[TestClass]
public class UnitTests
{
    [TestMethod]
    public async Task CreateNewCustomer()
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

        Assert.AreEqual("test email by harry potter", ep.Response);
    }

    [TestMethod]
    public async Task AdminLoginSuccess()
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
        Assert.IsFalse(ep.ValidationFailed);
        Assert.IsNotNull(rsp);
        Assert.IsTrue(rsp.Permissions.Contains("Inventory_Delete_Item"));
    }

    [TestMethod]
    public async Task AdminLoginWithBadInput()
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
        Assert.IsTrue(ep.ValidationFailed);
        Assert.IsTrue(ep.ValidationFailures.Any(f => f.ErrorMessage == "Authentication Failed!"));
    }

    [TestMethod]
    public async Task ListRecentCustomers()
    {
        var res = await Factory
            .Create<Customers.List.Recent.Endpoint>()
            .ExecuteAsync(default) as Customers.List.Recent.Response;

        Assert.AreEqual(3, res?.Customers?.Count());
        Assert.AreEqual("ryan gunner", res?.Customers?.First().Key);
        Assert.AreEqual("ryan reynolds", res?.Customers?.Last().Key);
    }
}
