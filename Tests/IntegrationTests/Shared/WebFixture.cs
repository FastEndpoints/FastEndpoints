using FastEndpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Mocks;
using System.Net.Http.Headers;
using Web.Services;
using Xunit.Abstractions;

namespace Shared;

public sealed class WebFixture : IDisposable
{
    public HttpClient GuestClient { get; init; }
    public HttpClient AdminClient { get; init; }
    public HttpClient CustomerClient { get; init; }
    public HttpClient RangeClient { get; init; }

    private static readonly WebApplicationFactory<Web.Program> _factory = new();

    private readonly IMessageSink _messageSink;
    bool disposedValue;

    public WebFixture(IMessageSink messageSink)
    {
        _messageSink = messageSink;

        GuestClient = CreateClient(svc => svc.AddSingleton<IEmailService, EmailService>());

        AdminClient = CreateClient(
            svc => svc.AddSingleton<IEmailService, MockEmailService>(),
            cln =>
            {
                var (_, result) = GuestClient.POSTAsync<
                    Admin.Login.Endpoint,
                    Admin.Login.Request,
                    Admin.Login.Response>(new()
                    {
                        UserName = "admin",
                        Password = "pass"
                    }).GetAwaiter().GetResult();

                cln.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
                cln.DefaultRequestHeaders.Add("tenant-id", "admin");
            });

        CustomerClient = CreateClient(
            svc => svc.AddSingleton<IEmailService, EmailService>(),
            cln =>
            {
                var (_, customerToken) = GuestClient.GETAsync<Customers.Login.Endpoint, string>().GetAwaiter().GetResult();

                cln.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
                cln.DefaultRequestHeaders.Add("tenant-id", "qwerty");
                cln.DefaultRequestHeaders.Add("CustomerID", "123");
            });

        RangeClient = CreateClient(
            svc => svc.AddSingleton<IEmailService, EmailService>(),
            cln => cln.DefaultRequestHeaders.Range = new(5, 9));
    }

    public HttpClient CreateClient(Action<IServiceCollection>? services = null, Action<HttpClient>? client = null)
    {
        var c = _factory.WithWebHostBuilder(b =>
        {
            if (services is not null) b.ConfigureTestServices(services);
            b.ConfigureLogging(c => c.AddXUnit(_messageSink));
        }).CreateClient();

        client?.Invoke(c);

        return c;
    }

#pragma warning disable CA1822

    public HttpMessageHandler CreateHttpMessageHandler()
        => _factory.Server.CreateHandler();

    public IServiceProvider GetServiceProvider()
        => _factory.Services;

    #region idisposable
    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                GuestClient.Dispose();
                AdminClient.Dispose();
                CustomerClient.Dispose();
                RangeClient.Dispose();
                // WARNING: do not dispose _factory
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
