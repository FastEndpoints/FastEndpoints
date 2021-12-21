using Web.PipelineBehaviors.PostProcessors;
using Web.PipelineBehaviors.PreProcessors;
using Web.SystemEvents;

namespace Sales.Orders.Create;

public class Endpoint : Endpoint<Request, Response, DomainEntity>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/sales/orders/create/{GuidTest}");
        PreProcessors(
            new MyRequestLogger<Request>());
        PostProcessors(
            new MyResponseLogger<Request, Response>());
    }

    public override async Task HandleAsync(Request r, CancellationToken t)
    {
        var userType = User.ClaimValue(Claim.UserType);

        var domainEntity = MapToEntity(r);

        var saleNotification = new NewOrderCreated
        {
            CustomerName = $"new customer ({userType})",
            OrderID = Random.Shared.Next(0, 10000),
            OrderTotal = 12345.67m
        };

        await PublishAsync(saleNotification, Mode.WaitForNone);

        await SendAsync(new Response
        {
            Message = "order created!",
            OrderID = 54321,
            GuidTest = r.GuidTest
        });
    }

    protected override DomainEntity MapToEntity(Request r) => new()
    {
        OrderNumber = r.GuidTest.ToString(),
        Price = 100.00m,
        Quantity = r.Quantity
    };
}