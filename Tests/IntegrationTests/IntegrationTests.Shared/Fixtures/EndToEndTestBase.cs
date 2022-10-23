using FastEndpoints;
using IntegrationTests.Shared.Mocks;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Web.Services;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Shared.Fixtures;

public abstract class EndToEndTestBase : IClassFixture<EndToEndTestFixture>
{
    protected EndToEndTestFixture EndToEndTestFixture { get; }
    protected HttpClient AdminClient { get; }
    protected HttpClient GuestClient { get; }
    protected HttpClient CustomerClient { get; }
    protected HttpClient RangeClient { get; }

    protected EndToEndTestBase(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper)
    {
        EndToEndTestFixture = endToEndTestFixture;

        AdminClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, MockEmailService>());

        GuestClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());

        CustomerClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());

        RangeClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());

        EndToEndTestFixture.SetOutputHelper(outputHelper);

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