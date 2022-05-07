using System.Net.Http.Headers;
using FastEndpoints;
using IntegrationTests.Shared.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Web.Services;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Shared.Fixtures;

public abstract class IntegrationTestBase : IClassFixture<IntegrationTestFixture>
{
    protected CancellationTokenSource CancellationTokenSource { get; } = new(TimeSpan.FromSeconds(10));
    protected IServiceProvider ServiceProvider { get; }
    protected IServiceScope Scope { get; }
    protected IntegrationTestFixture IntegrationTestFixture { get; }

    protected CancellationToken CancellationToken => CancellationTokenSource.Token;
    protected TextWriter TextWriter => Scope.ServiceProvider.GetRequiredService<TextWriter>();

    protected HttpClient AdminClient { get; }
    protected HttpClient GuestClient { get; }
    protected HttpClient CustomerClient { get; }
    protected HttpClient RangeClient { get; }

    protected IntegrationTestBase(IntegrationTestFixture integrationTestFixture, ITestOutputHelper outputHelper)
    {
        IntegrationTestFixture = integrationTestFixture;

        AdminClient = IntegrationTestFixture.CreateNewClient(services =>
        {
            services.AddSingleton<IEmailService, MockEmailService>();
        });

        GuestClient = IntegrationTestFixture.CreateNewClient(services =>
        {
            services.AddSingleton<IEmailService, EmailService>();
        });

        CustomerClient = IntegrationTestFixture.CreateNewClient(services =>
        {
            services.AddSingleton<IEmailService, EmailService>();
        });

        RangeClient = IntegrationTestFixture.CreateNewClient(services =>
        {
            services.AddSingleton<IEmailService, EmailService>();
        });

        IntegrationTestFixture.SetOutputHelper(outputHelper);
        ServiceProvider = IntegrationTestFixture.ServiceProvider;
        Scope = ServiceProvider.CreateScope();

        var (_, result) = GuestClient.POSTAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                Admin.Login.Response>(new()
            {
                UserName = "admin",
                Password = "pass"
            })
            .GetAwaiter()
            .GetResult();

        var (_, customerToken) = GuestClient.GETAsync<
                Customers.Login.Endpoint,
                string>()
            .GetAwaiter().GetResult();

        AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
        AdminClient.DefaultRequestHeaders.Add("tenant-id", "admin");
        CustomerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        CustomerClient.DefaultRequestHeaders.Add("tenant-id", "qwerty");
        CustomerClient.DefaultRequestHeaders.Add("CustomerID", "123");
        RangeClient.DefaultRequestHeaders.Range = new(5, 9);
    }
}