using FakeItEasy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Xunit;

namespace ResponseCaching;

public class ResponseCacheExecutorTests
{
    [Fact]
    public void EndpointWithoutResponseCaching_IsLeftAlone()
    {
        var ctx = new DefaultHttpContext(); //deliberately without the caching feature - the null check comes first
        var def = NewDefinition();

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.CacheControl.Count.ShouldBe(0);
        def.CachedCacheControl.ShouldBeNull();
    }

    [Fact]
    public void MissingCachingMiddleware_Throws()
    {
        var def = NewDefinition();
        def.ResponseCache(60);

        var ex = Should.Throw<InvalidOperationException>(() => ResponseCacheExecutor.Execute(new DefaultHttpContext(), def));

        ex.Message.ShouldBe("Please enable response caching middleware!");
    }

    [Fact]
    public void ZeroDurationWithoutNoStore_Throws()
    {
        var def = NewDefinition();
        def.ResponseCache(0);

        var ex = Should.Throw<InvalidOperationException>(() => ResponseCacheExecutor.Execute(NewContext(), def));

        ex.Message.ShouldBe("ResponseCache duration MUST be set unless NoStore is true!");
    }

    [Theory]
    [InlineData(ResponseCacheLocation.Any, "public,max-age=60")]
    [InlineData(ResponseCacheLocation.Client, "private,max-age=60")]
    [InlineData(ResponseCacheLocation.None, "no-cache,max-age=60")]
    [InlineData((ResponseCacheLocation)99, "max-age=60")] //out of range values fall back to a bare max-age
    public void CacheControl_IsEmittedForEachLocation(ResponseCacheLocation location, string expected)
    {
        var ctx = NewContext();
        var def = NewDefinition();
        def.ResponseCache(60, location);

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.CacheControl.ToString().ShouldBe(expected);
        ctx.Response.Headers.Pragma.ToString().ShouldBe(location == ResponseCacheLocation.None ? "no-cache" : string.Empty);
    }

    [Fact]
    public void NoStore_EmitsNoStoreWithoutMaxAge()
    {
        var ctx = NewContext();
        var def = NewDefinition();
        def.ResponseCache(0, ResponseCacheLocation.Any, noStore: true);

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        ctx.Response.Headers.Pragma.Count.ShouldBe(0);
        def.CachedCacheControl.ShouldBeNull(); //the no-store branch emits constants, so nothing needs caching
    }

    [Fact]
    public void NoStoreWithLocationNone_AppendsNoCacheAndPragma()
    {
        var ctx = NewContext();
        var def = NewDefinition();
        def.ResponseCache(0, ResponseCacheLocation.None, noStore: true);

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.CacheControl.ToString().ShouldBe("no-store,no-cache");
        ctx.Response.Headers.Pragma.ToString().ShouldBe("no-cache");
    }

    [Fact]
    public void VaryByHeaderAndQueryKeys_AreApplied()
    {
        var feature = A.Fake<IResponseCachingFeature>();
        var ctx = NewContext(feature);
        var def = NewDefinition();
        def.ResponseCache(60, varyByHeader: "X-Tenant", varyByQueryKeys: ["page"]);

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.Vary.ToString().ShouldBe("X-Tenant");
        feature.VaryByQueryKeys.ShouldBe(["page"]);
    }

    [Fact]
    public void CacheControlValue_IsBuiltOnceAndReusedAcrossRequests()
    {
        var def = NewDefinition();
        def.ResponseCache(60, ResponseCacheLocation.Client);

        var first = new DefaultHttpContext();
        first.Features.Set<IResponseCachingFeature>(A.Fake<IResponseCachingFeature>());
        ResponseCacheExecutor.Execute(first, def);

        var cached = def.CachedCacheControl;
        cached.ShouldBe("private,max-age=60");

        var second = NewContext();
        ResponseCacheExecutor.Execute(second, def);

        //the same string instance is handed to every subsequent request instead of being interpolated again
        ReferenceEquals(second.Response.Headers.CacheControl.ToString(), cached).ShouldBeTrue();
        def.CachedCacheControl.ShouldBeSameAs(cached);
    }

    [Fact]
    public void StaleHeadersFromEarlierMiddleware_AreReplaced()
    {
        var ctx = NewContext();
        ctx.Response.Headers.CacheControl = "public,max-age=999";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Vary = "X-Old";

        var def = NewDefinition();
        def.ResponseCache(30, ResponseCacheLocation.Any);

        ResponseCacheExecutor.Execute(ctx, def);

        ctx.Response.Headers.CacheControl.ToString().ShouldBe("public,max-age=30");
        ctx.Response.Headers.Pragma.Count.ShouldBe(0);
        ctx.Response.Headers.Vary.Count.ShouldBe(0);
    }

    static EndpointDefinition NewDefinition()
        => new(typeof(object), typeof(object), typeof(object));

    static DefaultHttpContext NewContext(IResponseCachingFeature? feature = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Features.Set(feature ?? A.Fake<IResponseCachingFeature>());

        return ctx;
    }
}
