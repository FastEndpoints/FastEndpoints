using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Binding;

public class MaxRequestBodyLimitTests : IAsyncLifetime
{
    readonly WebApplication _app;
    readonly IServiceResolver? _previousServiceResolver;

    public MaxRequestBodyLimitTests()
    {
        // Save the current ServiceResolver.Instance before UseFastEndpoints() overwrites it.
        // This prevents leaving a disposed IServiceProvider in the static ServiceResolver.Instance
        // after this test's WebApplication is disposed, which would cause ObjectDisposedException
        // in other tests that depend on ServiceResolver.Instance.
        _previousServiceResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;

        var bld = WebApplication.CreateBuilder();
        bld.WebHost.ConfigureKestrel(o => o.ListenLocalhost(1024));
        bld.Services.AddFastEndpoints(o => o.Filter = t => t == typeof(Endpoint));
        var app = bld.Build();
        app.UseFastEndpoints(
            c =>
            {
                c.Endpoints.Configurator = ep => ep.MaxRequestBodySize(100);
                c.Binding.FormExceptionTransformer = ex => new("formErrors", ex.Message);
            });
        _app = app;
    }

    public async ValueTask InitializeAsync()
        => await _app.StartAsync();

    [Fact, Trait("ExcludeInCiCd", "Yes")]
    public async Task Error_Response_When_Max_Req_Size_Exceeded()
    {
        var baseUrl = _app.Urls.First();

        var client = new HttpClient();
        client.BaseAddress = new(baseUrl);

        await using var stream = File.OpenRead("test.png");
        var req = new Request
        {
            File = new FormFile(stream, 0, stream.Length, "File1", "test.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            }
        };

        var (rsp, res) = await client.POSTAsync<Endpoint, Request, ErrorResponse>(req, sendAsFormData: true);
        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors["formErrors"][0].ShouldBe("Request body too large. The max request body size is 100 bytes.");
    }

    sealed class Request
    {
        public IFormFile File { get; set; } = default!;
    }

    sealed class Endpoint : Endpoint<Request, string>
    {
        public override void Configure()
        {
            Post("max-req-body-size-limit");
            AllowAnonymous();
            AllowFileUploads();
            MaxRequestBodySize(1);
        }

        public override async Task HandleAsync(Request r, CancellationToken c)
        {
            await Send.OkAsync(r.File.FileName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();

        // Restore the previous ServiceResolver.Instance so that other tests
        // don't end up using a disposed IServiceProvider.
        if (_previousServiceResolver is not null)
            ServiceResolver.Instance = _previousServiceResolver;
    }
}