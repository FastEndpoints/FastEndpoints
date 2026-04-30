using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Binding;

[Trait("ExcludeInCiCd", "Yes")]
public class MalformedMultipartTests : IAsyncLifetime
{
    const string MalformedFormDataError = "The request body contains malformed form data!";

    readonly WebApplication _app;
    readonly IServiceResolver? _previousServiceResolver;
    readonly string? _previousRoutePrefix;
    readonly Func<EndpointDefinition, bool>? _previousEndpointFilter;
    readonly Action<EndpointDefinition>? _previousEndpointConfigurator;

    public MalformedMultipartTests()
    {
        _previousServiceResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
        _previousRoutePrefix = Config.EpOpts.RoutePrefix;
        _previousEndpointFilter = Config.EpOpts.Filter;
        _previousEndpointConfigurator = Config.EpOpts.Configurator;

        var bld = WebApplication.CreateBuilder();
        bld.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        bld.Services.AddFastEndpoints(o => o.Filter = t => t == typeof(Ep));
        var app = bld.Build();
        app.UseFastEndpoints(
            c =>
            {
                c.Endpoints.RoutePrefix = null;
                c.Endpoints.Filter = null;
                c.Endpoints.Configurator = null;
            });
        _app = app;
    }

    public async ValueTask InitializeAsync()
        => await _app.StartAsync();

    [Fact]
    public async Task Truncated_Multipart_Body_Returns_400()
    {
        var baseUrl = _app.Urls.First();
        var client = new HttpClient { BaseAddress = new(baseUrl) };

        var request = new HttpRequestMessage(HttpMethod.Post, "malformed-multipart-test");
        var content = new StringContent("------boundary\r\nContent-Disposition: form-data; name=\"file\"; filename=\"test.jpg\"\r\nContent-Type: image/jpeg\r\n\r\nINCOMPLETE");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=----boundary");
        request.Content = content;

        var rsp = await client.SendAsync(request);
        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var res = await rsp.Content.ReadFromJsonAsync<ErrorResponse>();
        res.ShouldNotBeNull();
        res.Errors["generalErrors"].ShouldBe([MalformedFormDataError]);
    }

    [Fact]
    public async Task Malformed_Content_Disposition_Returns_400()
    {
        var baseUrl = _app.Urls.First();
        var client = new HttpClient { BaseAddress = new(baseUrl) };

        var request = new HttpRequestMessage(HttpMethod.Post, "malformed-multipart-test");
        var content = new StringContent("------boundary\r\nContent-Disposition: form-data; name=\"' OR '1'='1\r\n\r\ndata\r\n------boundary--");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=----boundary");
        request.Content = content;

        var rsp = await client.SendAsync(request);
        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await rsp.Content.ReadAsStringAsync();
        body.ShouldNotContain("' OR '1'='1");

        var res = JsonSerializer.Deserialize<ErrorResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        res.ShouldNotBeNull();
        res.Errors["generalErrors"].ShouldBe([MalformedFormDataError]);
    }

    sealed class Req
    {
        public IFormFile File { get; set; } = default!;
    }

    sealed class Ep : Endpoint<Req, string>
    {
        public override void Configure()
        {
            Post("malformed-multipart-test");
            AllowAnonymous();
            AllowFileUploads();
        }

        public override async Task HandleAsync(Req r, CancellationToken c)
        {
            await Send.OkAsync("ok");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();

        Config.EpOpts.RoutePrefix = _previousRoutePrefix;
        Config.EpOpts.Filter = _previousEndpointFilter;
        Config.EpOpts.Configurator = _previousEndpointConfigurator;

        if (_previousServiceResolver is not null)
            ServiceResolver.Instance = _previousServiceResolver;
    }
}
