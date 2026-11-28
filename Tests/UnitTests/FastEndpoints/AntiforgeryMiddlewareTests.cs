using FastEndpoints;
using FakeItEasy;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Security;

public sealed class AntiforgeryMiddlewareTests : IDisposable
{
    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/x-www-form-urlencoded; charset=utf-8")]
    [InlineData("application/x-www-form-urlencoded;charset=UTF-8")]
    [InlineData("APPLICATION/X-WWW-FORM-URLENCODED")]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("multipart/form-data; boundary=\"unterminated")]
    [InlineData("application/json", "application/json")]
    [InlineData("application/json; charset=utf-8", "application/json")]
    [InlineData("Application/JSON;charset=UTF-8", "application/json")]
    [InlineData("application/json; charset=utf-8", "application/json; charset=utf-8")]
    [InlineData("application/json", "application/json; charset=utf-8")]
    [InlineData("application/json;charset=UTF-8", "application/json; charset=utf-8")]
    [InlineData("not a media type", "not a media type")]
    public async Task Form_And_Additional_Content_Types_Are_Validated(string contentType, string? additionalContentType = null)
    {
        if (additionalContentType is not null)
            AntiforgeryMiddleware.AdditionalContentTypes = [additionalContentType];

        var (antiforgery, ctx, nextCalled) = await Invoke(HttpMethods.Post, contentType);

        A.CallTo(() => antiforgery.ValidateRequestAsync(ctx)).MustHaveHappenedOnceExactly();
        nextCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("POST", "application/json")]
    [InlineData("POST", "application/json; charset=utf-8")]
    [InlineData("POST", "text/plain; charset=utf-8")]
    [InlineData("POST", "not a media type")]
    [InlineData("POST", "")]
    [InlineData("GET", "application/x-www-form-urlencoded; charset=utf-8")]
    [InlineData("GET", "text/plain", typeof(PlainTextRequest))]
    public async Task Other_Requests_Are_Not_Validated(string method, string contentType, Type? requestDtoType = null)
    {
        var (antiforgery, _, nextCalled) = await Invoke(method, contentType, requestDtoType);

        A.CallTo(antiforgery).MustNotHaveHappened();
        nextCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("application/json")]
    [InlineData("application/octet-stream")]
    [InlineData("not a media type")]
    [InlineData("")]
    public async Task Plain_Text_Requests_Are_Validated_Regardless_Of_Content_Type(string contentType)
    {
        var (antiforgery, ctx, nextCalled) = await Invoke(HttpMethods.Post, contentType, typeof(PlainTextRequest));

        A.CallTo(() => antiforgery.ValidateRequestAsync(ctx)).MustHaveHappenedOnceExactly();
        nextCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("application/x-www-form-urlencoded", typeof(EmptyRequest))]
    [InlineData("text/plain", typeof(PlainTextRequest))]
    public async Task Endpoints_Without_Antiforgery_Are_Not_Validated(string contentType, Type requestDtoType)
    {
        var (antiforgery, _, nextCalled) = await Invoke(HttpMethods.Post, contentType, requestDtoType, enableAntiforgery: false);

        A.CallTo(antiforgery).MustNotHaveHappened();
        nextCalled.ShouldBeTrue();
    }

    static async Task<(IAntiforgery antiforgery, HttpContext ctx, bool nextCalled)> Invoke(string method,
                                                                                         string contentType,
                                                                                         Type? requestDtoType = null,
                                                                                         bool enableAntiforgery = true)
    {
        var def = new EndpointDefinition(typeof(AntiforgeryMiddlewareTests), requestDtoType ?? typeof(EmptyRequest), typeof(object));
        if (enableAntiforgery)
            def.EnableAntiforgery();

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.ContentType = contentType;
        ctx.SetEndpoint(new(null, new(def), "test"));

        var nextCalled = false;
        var antiforgery = A.Fake<IAntiforgery>();
        var middleware = new AntiforgeryMiddleware(
            _ =>
            {
                nextCalled = true;

                return Task.CompletedTask;
            },
            antiforgery);

        await middleware.Invoke(ctx);

        return (antiforgery, ctx, nextCalled);
    }

    public void Dispose()
        => AntiforgeryMiddleware.AdditionalContentTypes = [];
}
