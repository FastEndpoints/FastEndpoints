using System.Net;
using System.Text.Json;
using NativeAotChecker.Endpoints.Errors;

namespace NativeAotCheckerTests;

public class ExceptionHandlerTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Default_Exception_Handler_Returns_Internal_Error_Response()
    {
        using var rsp = await app.Client.GetAsync("unhandled-exception", Cancellation);
        var body = await rsp.Content.ReadAsStringAsync(Cancellation);

        rsp.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("code").GetInt32().ShouldBe(500);
        json.RootElement.GetProperty("reason").GetString().ShouldBe(UnhandledExceptionEndpoint.Reason);
    }
}
