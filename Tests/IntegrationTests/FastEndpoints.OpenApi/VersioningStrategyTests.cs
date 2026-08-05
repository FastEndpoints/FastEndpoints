namespace OpenApi;

/// <summary>
/// Integration coverage for OpenAPI versioning strategies against the Web harness.
/// Complements golden-file snapshots with explicit path assertions for release-group
/// and ad hoc (EndpointFilter) behavior.
/// </summary>
public class VersioningStrategyTests(Fixture App) : TestBase<Fixture>
{
    // ── release group strategy (MaxEndpointVersion) ──

    [Fact]
    public async Task initial_release_keeps_unversioned_login_only()
    {
        var paths = await GetPathsAsync("Initial Release");

        paths.ShouldContain("/api/admin/login");
        paths.ShouldNotContain("/api/admin/login/ver1");
        paths.ShouldNotContain("/api/admin/login/ver2");
    }

    [Fact]
    public async Task release_1_keeps_latest_login_and_customer_list_within_max_version()
    {
        // MaxEndpointVersion = 1 → latest ≤ 1 per bare route (not frozen historical snapshots)
        var paths = await GetPathsAsync("Release 1.0");

        paths.ShouldContain("/api/admin/login/ver1");
        paths.ShouldNotContain("/api/admin/login");
        paths.ShouldNotContain("/api/admin/login/ver2");

        paths.ShouldContain("/api/customer/list/recent/ver1");
        paths.ShouldNotContain("/api/customer/list/recent");
        paths.ShouldNotContain("/api/customer/list/recent/ver2");
        paths.ShouldNotContain("/api/customer/list/recent/ver3");
    }

    [Fact]
    public async Task release_2_with_show_deprecated_includes_prior_versions_in_range()
    {
        // Release 2.0 sets MaxEndpointVersion = 2 and ShowDeprecatedOps = true
        var paths = await GetPathsAsync("Release 2.0");

        paths.ShouldContain("/api/admin/login/ver2");
        paths.ShouldContain("/api/admin/login/ver1");
        paths.ShouldContain("/api/admin/login");

        paths.ShouldContain("/api/customer/list/recent/ver2");
        paths.ShouldContain("/api/customer/list/recent/ver1");
        paths.ShouldContain("/api/customer/list/recent");
    }

    // ── ad hoc grouping (EndpointFilter) + default MaxEndpointVersion ──

    [Fact]
    public async Task adhoc_filter_document_without_max_excludes_versioned_endpoints()
    {
        // "Swagger Review" uses EndpointFilter only (MaxEndpointVersion defaults to 0).
        // VersionPrefilterV1Endpoint is tagged swagger_review and calls Version(1).
        var paths = await GetPathsAsync("Swagger Review");

        paths.ShouldContain("/api/swagger-review/version-prefilter-initial");
        paths.ShouldNotContain("/api/swagger-review/version-prefilter-v1");
        paths.ShouldNotContain("/api/swagger-review/version-prefilter-v1/ver1");
    }

    [Fact]
    public async Task release_group_with_max_1_includes_versioned_prefilter_endpoint()
    {
        // Same Version(1) endpoint appears once MaxEndpointVersion admits it.
        var paths = await GetPathsAsync("Release 1.0");

        paths.ShouldContain("/api/swagger-review/version-prefilter-v1/ver1");
        paths.ShouldContain("/api/swagger-review/version-prefilter-initial");
    }

    // ── release version strategy (orthogonal; sanity on dedicated docs) ──

    [Fact]
    public async Task release_version_strategy_includes_endpoints_by_starting_release()
    {
        var v1 = await GetPathsAsync("ReleaseVersioning - v1");
        var v2 = await GetPathsAsync("ReleaseVersioning - v2");

        // EndpointB_V1 uses StartingRelease(2) → absent from release 1, present from release 2
        v1.ShouldNotContain(p => p.Contains("endpoint-b", StringComparison.Ordinal) && p.Contains("ver1", StringComparison.Ordinal));
        v2.ShouldContain("/api/release-versioning/endpoint-b/ver1");

        // EndpointA_V1 uses Version(1) with default StartingRelease=1 → present from release 1
        v1.ShouldContain("/api/release-versioning/endpoint-a/ver1");
    }

    async Task<HashSet<string>> GetPathsAsync(string documentName)
    {
        var json = await App.GetDocumentJsonAsync(documentName);
        var paths = JsonNode.Parse(json)!["paths"] as JsonObject;

        return paths?.Select(p => p.Key).ToHashSet(StringComparer.Ordinal)
               ?? new HashSet<string>(StringComparer.Ordinal);
    }
}
