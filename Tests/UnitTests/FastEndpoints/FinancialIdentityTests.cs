using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialIdentityTests
{
    [Fact]
    public void Prefix_Is_Distinct_From_Fingerprint()
    {
        var key = FinancialIdentity.Build(Request(), new() { CallerScope = _ => "account" });

        key.ShouldStartWith("FE:FIDMP:");
        key.Length.ShouldBe("FE:FIDMP:".Length + 64);
    }

    [Fact]
    public void Method_Scheme_Host_Are_Case_Insensitive()
    {
        var a = Request(method: "post", scheme: "https", host: "Example.COM:443");
        var b = Request(method: "POST", scheme: "HTTPS", host: "example.com:443");

        FinancialIdentity.Build(a, new() { CallerScope = _ => "account" }).ShouldBe(FinancialIdentity.Build(b, new() { CallerScope = _ => "account" }));
    }

    [Fact]
    public void Path_Is_Case_Insensitive_By_Default()
    {
        var a = Request(path: "/Charges");
        var b = Request(path: "/charges");

        FinancialIdentity.Build(a, new() { CallerScope = _ => "account" }).ShouldBe(FinancialIdentity.Build(b, new() { CallerScope = _ => "account" }));
    }

    [Fact]
    public void Path_Casing_Is_Identity_When_Enabled()
    {
        var opts = new FinancialIdempotencyOptions { CallerScope = _ => "account", UseCaseSensitivePaths = true };
        var a = Request(path: "/Charges");
        var b = Request(path: "/charges");

        FinancialIdentity.Build(a, opts).ShouldNotBe(FinancialIdentity.Build(b, opts));
    }

    [Theory]
    [InlineData("", "/charges", "/charges/")]
    [InlineData("/base", "/charges", "/charges/")]
    [InlineData("", "", "/")]
    [InlineData("/base", "", "/")]
    public void Single_Trailing_Slash_Is_Not_Identity(string pathBase, string first, string second)
    {
        var a = Request(path: first);
        var b = Request(path: second);
        a.PathBase = b.PathBase = pathBase;

        foreach (var caseSensitive in new[] { false, true })
        {
            var opts = new FinancialIdempotencyOptions { CallerScope = _ => "account", UseCaseSensitivePaths = caseSensitive };
            FinancialIdentity.Build(a, opts).ShouldBe(FinancialIdentity.Build(b, opts));
        }
    }

    [Theory]
    [InlineData("/charges/", "/charges//")]
    [InlineData("/charges//", "/charges///")]
    [InlineData("/charges/1", "/charges//1")]
    [InlineData("/charges/1/", "/charges/2/")]
    [InlineData("/", "//")]
    public void Distinct_Resource_Paths_Are_Not_Collapsed(string first, string second)
    {
        var opts = new FinancialIdempotencyOptions { CallerScope = _ => "account" };
        FinancialIdentity.Build(Request(path: first), opts).ShouldNotBe(FinancialIdentity.Build(Request(path: second), opts));
    }

    [Fact]
    public void Query_Is_Not_Identity()
    {
        var a = Request(query: "?x=1");
        var b = Request(query: "?x=2");

        FinancialIdentity.Build(a, new() { CallerScope = _ => "account" }).ShouldBe(FinancialIdentity.Build(b, new() { CallerScope = _ => "account" }));
    }

    [Fact]
    public void Authorization_Change_Does_Not_Change_Identity()
    {
        var a = Request();
        a.Headers.Authorization = "Bearer one";
        var b = Request();
        b.Headers.Authorization = "Bearer two";

        FinancialIdentity.Build(a, new() { CallerScope = _ => "account" }).ShouldBe(FinancialIdentity.Build(b, new() { CallerScope = _ => "account" }));
    }

    [Fact]
    public void User_Agent_Is_Never_Identity()
    {
        var opts = new FinancialIdempotencyOptions { CallerScope = _ => "account" };
        opts.AdditionalHeaders.Add("User-Agent");

        var a = Request();
        a.Headers.UserAgent = "ua-1";
        var b = Request();
        b.Headers.UserAgent = "ua-2";

        FinancialIdentity.Build(a, opts).ShouldBe(FinancialIdentity.Build(b, opts));
    }

    [Fact]
    public void Idempotency_Key_Is_Always_Identity()
    {
        var a = Request();
        a.Headers["Idempotency-Key"] = "k1";
        var b = Request();
        b.Headers["Idempotency-Key"] = "k2";

        FinancialIdentity.Build(a, new() { CallerScope = _ => "account" }).ShouldNotBe(FinancialIdentity.Build(b, new() { CallerScope = _ => "account" }));
    }

    [Fact]
    public void Caller_Scope_And_MultiValue_Framing_Are_Distinct()
    {
        var a = Request();
        var b = Request();
        var options = new FinancialIdempotencyOptions { AdditionalHeaders = ["X-Account"] };
        FinancialIdentity.Build(a, options, "one").ShouldNotBe(FinancialIdentity.Build(a, options, "two"));
        a.Headers["X-Account"] = new Microsoft.Extensions.Primitives.StringValues(["a", "b"]);
        b.Headers["X-Account"] = "a,b";
        FinancialIdentity.Build(a, options, "one").ShouldNotBe(FinancialIdentity.Build(b, options, "one"));
    }

    [Fact]
    public void Compiled_Identity_Preserves_Encoding_And_Header_Normalization()
    {
        var request = Request();
        request.Headers["Idempotency-Key"] = "key-1";
        request.Headers["X-Account"] = new Microsoft.Extensions.Primitives.StringValues(["a", "b"]);
        request.Headers["X-Zone"] = "west";
        request.Headers.UserAgent = "ignored";
        var options = new FinancialIdempotencyOptions
        {
            CallerScope = _ => "account",
            AdditionalHeaders = ["X-Zone", "x-account", "X-Account", "idempotency-key", "User-Agent", "Accept", "Content-Type", "Host"]
        };
        const string expected = "FE:FIDMP:73C6FE60D6246B1C9F18CAE3A6581E4CD4D00A95B08D024386252236376C4911";

        FinancialIdentity.Build(request, options).ShouldBe(expected);
        options.ApplyDefaults(new());
        FinancialIdentity.Build(request, options).ShouldBe(expected);
    }

    [Fact]
    public void Header_Changes_Are_Uncached_Until_Mapping_And_Recompiled_When_Defaults_Are_Reapplied()
    {
        var request = Request();
        request.Headers["Idempotency-Key"] = "original";
        request.Headers.UserAgent = "replacement";
        request.Headers["X-Account"] = "account";
        var options = new FinancialIdempotencyOptions { CallerScope = _ => "account" };
        var original = FinancialIdentity.Build(request, options);
        options.AdditionalHeaders.Add("X-Account");
        var additional = FinancialIdentity.Build(request, options);
        additional.ShouldNotBe(original);
        options.ApplyDefaults(new());
        FinancialIdentity.Build(request, options).ShouldBe(additional);

        options.HeaderName = "User-Agent";
        options.AdditionalHeaders.Clear();
        FinancialIdentity.Build(request, options).ShouldBe(additional);
        options.ApplyDefaults(new());
        var rebuilt = FinancialIdentity.Build(request, options);
        rebuilt.ShouldNotBe(additional);
        rebuilt.ShouldBe(FinancialIdentity.Build(request, new() { CallerScope = _ => "account", HeaderName = "User-Agent" }));
        request.Headers.UserAgent = "changed";
        FinancialIdentity.Build(request, options).ShouldNotBe(rebuilt);
    }

[Fact]
    public void Default_Port_Is_Not_A_Distinct_Origin()
    {
        var opts = new FinancialIdempotencyOptions { CallerScope = _ => "account" };
        string Id(string scheme, string host) => FinancialIdentity.Build(Request(scheme: scheme, host: host), opts, "account");

        Id("https", "example.com").ShouldBe(Id("https", "example.com:443"));
        Id("https", "example.com").ShouldBe(Id("HTTPS", "Example.COM:443"));
        Id("http", "example.com").ShouldBe(Id("http", "example.com:80"));
        Id("https", "[::1]").ShouldBe(Id("https", "[::1]:443"));
        Id("https", "example.com").ShouldNotBe(Id("http", "example.com"));
        Id("https", "example.com").ShouldNotBe(Id("https", "example.com:8443"));
        Id("https", "example.com").ShouldNotBe(Id("https", "example.com:80"));
        Id("https", "example.com").ShouldNotBe(Id("http", "example.com:443"));
        Id("https", "example.com:443").ShouldNotBe(Id("https", "example.com:444"));
    }

    static HttpRequest Request(string method = "POST",
                               string scheme = "https",
                               string host = "example.com",
                               string path = "/charges",
                               string? query = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Scheme = scheme;
        ctx.Request.Headers.Host = host;
        ctx.Request.Path = path;

        if (query is not null)
            ctx.Request.QueryString = new(query.StartsWith('?') ? query : "?" + query);

        return ctx.Request;
    }
}
