using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

/// <summary>
/// Focused coverage for release-group version filtering (MaxEndpointVersion / latest-in-range)
/// and version-range membership used by OpenAPI document generation.
/// </summary>
public class DocumentVersionFilterTests
{
    // ── release group: latest endpoint version ≤ MaxEndpointVersion per bare route ──

    [Fact]
    public void release_group_keeps_only_latest_version_within_max_per_bare_route()
    {
        // Docs "after another change": login v0/v1/v2 + order v0/v1
        // Release 1 (MaxEndpointVersion=1) must keep login/v1 and order/v1 (not unversioned order).
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/admin/login", "/admin/login", version: 0),
            Op("GET", "/admin/login", "/admin/login/v1", version: 1),
            Op("GET", "/admin/login", "/admin/login/v2", version: 2),
            Op("GET", "/order/{orderID}", "/order/{orderID}", version: 0),
            Op("GET", "/order/{orderID}", "/order/{orderID}/v1", version: 1));

        Apply(document, sharedCtx, maxEndpointVersion: 1);

        PathsOf(document).ShouldBe(
        [
            "/admin/login/v1",
            "/order/{orderID}/v1"
        ]);
    }

    [Fact]
    public void release_group_with_default_max_keeps_only_initial_versions()
    {
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/admin/login", "/admin/login", version: 0),
            Op("GET", "/admin/login", "/admin/login/v1", version: 1),
            Op("GET", "/order/{orderID}", "/order/{orderID}", version: 0),
            Op("GET", "/order/{orderID}", "/order/{orderID}/v1", version: 1));

        Apply(document, sharedCtx, maxEndpointVersion: 0);

        PathsOf(document).ShouldBe(
        [
            "/admin/login",
            "/order/{orderID}"
        ]);
    }

    [Fact]
    public void release_group_with_higher_max_picks_latest_per_route_independently()
    {
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/admin/login", "/admin/login", version: 0),
            Op("GET", "/admin/login", "/admin/login/v1", version: 1),
            Op("GET", "/admin/login", "/admin/login/v2", version: 2),
            Op("GET", "/order/{orderID}", "/order/{orderID}", version: 0),
            Op("GET", "/order/{orderID}", "/order/{orderID}/v1", version: 1));

        Apply(document, sharedCtx, maxEndpointVersion: 2);

        PathsOf(document).ShouldBe(
        [
            "/admin/login/v2",
            "/order/{orderID}/v1"
        ]);
    }

    [Fact]
    public void release_group_excludes_versions_above_max()
    {
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/admin/login", "/admin/login/v1", version: 1),
            Op("GET", "/admin/login", "/admin/login/v2", version: 2));

        Apply(document, sharedCtx, maxEndpointVersion: 1);

        PathsOf(document).ShouldBe(["/admin/login/v1"]);
    }

    [Fact]
    public void release_group_with_show_deprecated_keeps_all_versions_in_range()
    {
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/admin/login", "/admin/login", version: 0),
            Op("GET", "/admin/login", "/admin/login/v1", version: 1),
            Op("GET", "/admin/login", "/admin/login/v2", version: 2));

        Apply(document, sharedCtx, maxEndpointVersion: 2, showDeprecatedOps: true);

        PathsOf(document).ShouldBe(
        [
            "/admin/login",
            "/admin/login/v1",
            "/admin/login/v2"
        ]);
    }

    [Fact]
    public void release_group_hides_endpoint_when_deprecated_at_max()
    {
        // Version(1, deprecateAt: 2) is hidden when MaxEndpointVersion >= 2 and ShowDeprecatedOps is false.
        var (document, sharedCtx) = CreateDocumentWithOps(
            Op("GET", "/user/delete", "/user/delete/v1", version: 1, deprecatedAt: 2),
            Op("GET", "/user/profile", "/user/profile/v2", version: 2));

        Apply(document, sharedCtx, maxEndpointVersion: 2);

        PathsOf(document).ShouldBe(["/user/profile/v2"]);
    }

    // ── version range membership (also used when EndpointFilter is composed) ──

    [Theory]
    [InlineData(0, 0, true)]  // initial version, default max
    [InlineData(1, 0, false)] // Version(1) excluded when MaxEndpointVersion defaults to 0 (ad hoc footgun)
    [InlineData(1, 1, true)]
    [InlineData(0, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    public void is_in_requested_range_respects_max_endpoint_version(int endpointVersion, int maxEndpointVersion, bool expected)
    {
        var opts = new DocumentOptions { MaxEndpointVersion = maxEndpointVersion };

        // StartingRelease mirrors EndpointVersion(n) which sets StartingReleaseVersion = n
        DocumentVersionFilter.IsInRequestedRange(endpointVersion, startingReleaseVersion: endpointVersion, opts)
                            .ShouldBe(expected);
    }

    [Fact]
    public void includes_endpoint_requires_filter_and_version_range()
    {
        var def = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));
        def.EndpointVersion(1);
        def.Tags("GroupA");

        // Ad hoc as documented: filter only, MaxEndpointVersion defaults to 0
        var docsAsWritten = new DocumentOptions
        {
            EndpointFilter = ep => ep.EndpointTags?.Contains("GroupA") is true
        };
        DocumentVersionFilter.IncludesEndpoint(def, docsAsWritten).ShouldBeFalse();

        // Fixed: MaxEndpointVersion high enough for Version(1)
        var fixedOpts = new DocumentOptions
        {
            EndpointFilter = ep => ep.EndpointTags?.Contains("GroupA") is true,
            MaxEndpointVersion = 1
        };
        DocumentVersionFilter.IncludesEndpoint(def, fixedOpts).ShouldBeTrue();

        // Wrong tag excluded even when version is in range
        var wrongTag = new DocumentOptions
        {
            EndpointFilter = ep => ep.EndpointTags?.Contains("GroupB") is true,
            MaxEndpointVersion = 1
        };
        DocumentVersionFilter.IncludesEndpoint(def, wrongTag).ShouldBeFalse();
    }

    [Fact]
    public void min_and_max_endpoint_version_restrict_range()
    {
        var opts = new DocumentOptions
        {
            MinEndpointVersion = 3,
            MaxEndpointVersion = 3
        };

        DocumentVersionFilter.IsInRequestedRange(2, 2, opts).ShouldBeFalse();
        DocumentVersionFilter.IsInRequestedRange(3, 3, opts).ShouldBeTrue();
        DocumentVersionFilter.IsInRequestedRange(4, 4, opts).ShouldBeFalse();
    }

    // ── helpers ──

    sealed record OpSpec(string HttpMethod, string BareRoute, string DocumentPath, int Version, int DeprecatedAt);

    static OpSpec Op(string httpMethod, string bareRoute, string documentPath, int version, int deprecatedAt = 0)
        => new(httpMethod, bareRoute, documentPath, version, deprecatedAt);

    static (OpenApiDocument Document, SharedContext SharedCtx) CreateDocumentWithOps(params OpSpec[] ops)
    {
        var document = new OpenApiDocument { Paths = new OpenApiPaths() };
        var sharedCtx = new SharedContext();

        foreach (var op in ops)
        {
            if (!document.Paths.TryGetValue(op.DocumentPath, out var pathItem))
            {
                pathItem = new OpenApiPathItem { Operations = new Dictionary<HttpMethod, OpenApiOperation>() };
                document.Paths[op.DocumentPath] = pathItem;
            }

            var openApiMethod = HttpMethod.Parse(op.HttpMethod);
            pathItem.Operations![openApiMethod] = new OpenApiOperation { OperationId = op.DocumentPath };

            var dictionaryKey = $"{op.HttpMethod}:{op.DocumentPath}";
            sharedCtx.Operations[dictionaryKey] = new OperationMeta
            {
                OperationKey = $"{op.HttpMethod}:{op.BareRoute}",
                DocumentPath = op.DocumentPath,
                HttpMethod = op.HttpMethod,
                Version = op.Version,
                StartingReleaseVersion = op.Version,
                DeprecatedAt = op.DeprecatedAt,
                IsFastEndpoint = true
            };
        }

        return (document, sharedCtx);
    }

    static void Apply(OpenApiDocument document, SharedContext sharedCtx, int maxEndpointVersion, bool showDeprecatedOps = false)
    {
        var opts = new DocumentOptions
        {
            MaxEndpointVersion = maxEndpointVersion,
            ShowDeprecatedOps = showDeprecatedOps
        };
        new DocumentVersionFilter(opts, sharedCtx).Apply(document);
    }

    static string[] PathsOf(OpenApiDocument document)
        => document.Paths?.Keys.OrderBy(p => p, StringComparer.Ordinal).ToArray() ?? [];
}
