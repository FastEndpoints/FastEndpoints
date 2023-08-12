using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Shared.Mocks;
using System.Net.Http.Headers;
using Web.Services;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Fixtures;

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

        GuestClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());

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

        AdminClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, MockEmailService>());
        AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
        AdminClient.DefaultRequestHeaders.Add("tenant-id", "admin");

        var (_, customerToken) = GuestClient.GETAsync<
                Customers.Login.Endpoint,
                string>()
            .GetAwaiter().GetResult();

        CustomerClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());
        CustomerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        CustomerClient.DefaultRequestHeaders.Add("tenant-id", "qwerty");
        CustomerClient.DefaultRequestHeaders.Add("CustomerID", "123");

        RangeClient = EndToEndTestFixture.CreateNewClient(services => services.AddSingleton<IEmailService, EmailService>());
        RangeClient.DefaultRequestHeaders.Range = new(5, 9);

        EndToEndTestFixture.SetOutputHelper(outputHelper);
    }
}