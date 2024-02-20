using Messaging;
using TestCases.CommandBusTest;
using TestCases.EventBusTest;
using Web;
using Web.Services;

namespace Int.FastEndpoints;

public class AppFixture(IMessageSink s) : AppFixture<Web.Program>(s)
{
    public HttpClient GuestClient { get; private set; } = default!;
    public HttpClient AdminClient { get; private set; } = default!;
    public HttpClient CustomerClient { get; private set; } = default!;
    public HttpClient RangeClient { get; private set; } = default!;

    protected override async Task SetupAsync()
    {
        GuestClient = CreateClient();

        AdminClient = CreateClient();
        var (_, result) = await AdminClient.POSTAsync<
                              Admin.Login.Endpoint,
                              Admin.Login.Request,
                              Admin.Login.Response>(
                              new()
                              {
                                  UserName = "admin",
                                  Password = "pass"
                              });
        AdminClient.DefaultRequestHeaders.Authorization = new("Bearer", result?.JWTToken);
        AdminClient.DefaultRequestHeaders.Add("tenant-id", "admin");

        CustomerClient = CreateClient();
        var (_, customerToken) = await CustomerClient.GETAsync<Customers.Login.Endpoint, string>();
        CustomerClient.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        CustomerClient.DefaultRequestHeaders.Add("tenant-id", "qwerty");
        CustomerClient.DefaultRequestHeaders.Add("CustomerID", "123");

        RangeClient = CreateClient();
        RangeClient.DefaultRequestHeaders.Range = new(5, 9);
    }

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.RegisterTestCommandHandler<SomeCommand, TestCommandHandler, string>();
        s.RegisterTestCommandHandler<VoidCommand, TestVoidCommandHandler>();
        s.RegisterTestEventHandler<TestEventBus, FakeEventHandler>();
        s.RegisterTestEventHandler<TestEventBus, AnotherFakeEventHandler>();
        s.AddScoped<IEmailService, MockEmailService>();
    }

    protected override Task TearDownAsync()
    {
        GuestClient.Dispose();
        AdminClient.Dispose();
        CustomerClient.Dispose();
        RangeClient.Dispose();

        return Task.CompletedTask;
    }
}