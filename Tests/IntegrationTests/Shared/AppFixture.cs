using FastEndpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Mocks;
using System.Net.Http.Headers;
using Web;
using Web.Services;
using Xunit.Abstractions;

namespace Shared;

public sealed class AppFixture : WebApplicationFactory<Program>
{
    public HttpClient GuestClient { get; set; } = default!;
    public HttpClient AdminClient { get; set; } = default!;
    public HttpClient CustomerClient { get; set; } = default!;
    public HttpClient RangeClient { get; set; } = default!;

    private readonly IMessageSink _messageSink;

    public AppFixture(IMessageSink messageSink)
    {
        _messageSink = messageSink;
        InitClients();
    }

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.ConfigureLogging(l => l.AddXUnit(_messageSink));
        b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IEmailService, MockEmailService>();
        });
    }

    private void InitClients()
    {
        GuestClient = CreateClient();

        AdminClient = CreateClient();
        var (_, result) = GuestClient.POSTAsync<
            Admin.Login.Endpoint,
            Admin.Login.Request,
            Admin.Login.Response>(new()
            {
                UserName = "admin",
                Password = "pass"
            }).GetAwaiter().GetResult();

        AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
        AdminClient.DefaultRequestHeaders.Add("tenant-id", "admin");

        CustomerClient = CreateClient();
        var (_, customerToken) = GuestClient.GETAsync<Customers.Login.Endpoint, string>().GetAwaiter().GetResult();
        CustomerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        CustomerClient.DefaultRequestHeaders.Add("tenant-id", "qwerty");
        CustomerClient.DefaultRequestHeaders.Add("CustomerID", "123");

        RangeClient = CreateClient();
        RangeClient.DefaultRequestHeaders.Range = new(5, 9);
    }

    public HttpMessageHandler CreateHttpMessageHandler()
        => Server.CreateHandler();

    public IServiceProvider GetServiceProvider()
        => Services;

    public override async ValueTask DisposeAsync()
    {
        GuestClient.Dispose();
        AdminClient.Dispose();
        CustomerClient.Dispose();
        RangeClient.Dispose();

        foreach (var f in Factories)
            await f.DisposeAsync();
    }
}