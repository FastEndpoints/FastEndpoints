using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Binding;

public class MalformedMultipartTests : IAsyncLifetime
{
    readonly WebApplication _app;
    readonly IServiceResolver? _previousServiceResolver;

    public MalformedMultipartTests()
    {
        _previousServiceResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;

        var bld = WebApplication.CreateBuilder();
        bld.WebHost.ConfigureKestrel(o => o.ListenLocalhost(1025));
        bld.Services.AddFastEndpoints(o => o.Filter = t => t == typeof(Ep));
        var app = bld.Build();
        app.UseFastEndpoints();
        _app = app;
    }

    public async ValueTask InitializeAsync()
        => await _app.StartAsync();

    [Fact, Trait("ExcludeInCiCd", "Yes")]
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
        res.Errors.ShouldContainKey("formData");
    }

    [Fact, Trait("ExcludeInCiCd", "Yes")]
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

        var res = await rsp.Content.ReadFromJsonAsync<ErrorResponse>();
        res.ShouldNotBeNull();
        res.Errors.ShouldContainKey("formData");
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

        if (_previousServiceResolver is not null)
            ServiceResolver.Instance = _previousServiceResolver;
    }
}
