using TestCases.CommandBusTest;
using Web.PipelineBehaviors.PostProcessors;
using Web.PipelineBehaviors.PreProcessors;
using Web.Services;
using Web.SystemEvents;

namespace Sales.Orders.Create;

public class Endpoint : Endpoint<Request, Response, MyMapper>
{
    public IEmailService Emailer { get; set; }

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes(
            "/sales/orders/create/{guidTest}",
            "/sales/orders/create");
        PreProcessors(
            new MyRequestLogger<Request>());
        PostProcessors(
            new MyResponseLogger<Request, Response>());
        Tags("orders");
    }

    public async override Task HandleAsync(Request r, CancellationToken t)
    {
        var fullName = new TestCommand
        {
            FirstName = "x",
            LastName = "y"
        }
        .ExecuteAsync();

        var userType = User.ClaimValue(Claim.UserType);

        var saleNotification = new NewOrderCreated
        {
            CustomerName = $"new customer ({fullName}) ({userType})",
            OrderID = Random.Shared.Next(0, 10000),
            OrderTotal = 12345.67m,
        };

        await PublishAsync(saleNotification, Mode.WaitForNone);

        await SendAsync(new Response
        {
            Message = "order created!",
            AnotherMsg = Map.ToEntity(r),
            OrderID = 54321,
            GuidTest = r.GuidTest
        });
    }
}

public class MyMapper : Mapper<Request, Response, string>
{
    public override string ToEntity(Request r)
    {
        var x = Resolve<IEmailService>();
        return x.SendEmail();
    }
}