using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace OpenApi;

public class OperationTagTests
{
    [Fact]
    public void disabled_auto_tagging_keeps_nested_group_tags()
    {
        var operation = Operation("Last");
        var options = new DocumentOptions { AutoTagPathSegmentIndex = 0 };

        Apply(options, operation, GroupMetadata());

        ShouldHaveTags(operation, "First", "Middle", "Last");
    }

    [Fact]
    public void null_or_empty_operation_tags_are_filled_from_explicit_metadata()
    {
        var options = new DocumentOptions { AutoTagPathSegmentIndex = 0 };
        var missing = new OpenApiOperation();
        var empty = new OpenApiOperation { Tags = new HashSet<OpenApiTagReference>() };

        Apply(options, missing, GroupMetadata());
        Apply(options, empty, GroupMetadata());

        ShouldHaveTags(missing, "First", "Middle", "Last");
        ShouldHaveTags(empty, "First", "Middle", "Last");
    }

    [Fact]
    public void multiple_names_in_one_entry_and_repeated_withtags_are_kept()
    {
        var operation = new OpenApiOperation();
        var metadata = new List<object>
        {
            new TagsAttribute("Alpha", "Beta"),
            new TagsAttribute("Gamma"),
            new TagsAttribute("Beta", "Delta")
        };

        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, metadata);

        ShouldHaveTags(operation, "Alpha", "Beta", "Gamma", "Delta");
    }

    [Fact]
    public void repeated_identical_names_are_emitted_once()
    {
        var operation = Operation("Last");
        var metadata = new List<object>
        {
            new TagsAttribute("First", "First"),
            new TagsAttribute("Middle"),
            new TagsAttribute("Last", "Middle")
        };

        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, metadata);

        ShouldHaveTags(operation, "First", "Middle", "Last");
    }

    [Fact]
    public void case_variants_keep_caller_spelling()
    {
        var operation = Operation("LAST");
        var metadata = new List<object>
        {
            new TagsAttribute("First"),
            new TagsAttribute("first"),
            new TagsAttribute("Last")
        };

        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, metadata);
        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, metadata);

        ShouldHaveTags(operation, "LAST", "First", "first", "Last");
    }

    [Fact]
    public void dont_auto_tag_preserves_explicit_tags()
    {
        var operation = Operation("Web");

        Apply(new DocumentOptions(), operation, GroupMetadata(), dontAutoTag: true);

        ShouldHaveTags(operation, "First", "Middle", "Last");
    }

    [Fact]
    public void auto_tagging_adds_the_route_tag_beside_explicit_tags()
    {
        var operation = Operation("Last");

        Apply(new DocumentOptions(), operation, GroupMetadata(), "/orders/middle/last");

        ShouldHaveTags(operation, "First", "Middle", "Last", "Orders");
    }

    [Fact]
    public void auto_tag_matching_an_explicit_tag_is_not_duplicated()
    {
        var operation = Operation("Last");

        Apply(new DocumentOptions(), operation, GroupMetadata(), "/first/middle/last");
        Apply(new DocumentOptions(), operation, GroupMetadata(), "/first/middle/last");

        ShouldHaveTags(operation, "First", "Middle", "Last");
    }

    [Theory]
    [InlineData(TagCase.TitleCase, false, "Review-Tag")]
    [InlineData(TagCase.TitleCase, true, "ReviewTag")]
    [InlineData(TagCase.LowerCase, true, "reviewtag")]
    [InlineData(TagCase.None, true, "ReviewTag")]
    public void automatic_tag_formatting_leaves_explicit_spelling_unchanged(TagCase tagCase, bool stripSymbols, string automaticTag)
    {
        var options = new DocumentOptions
        {
            TagCase = tagCase,
            TagStripSymbols = stripSymbols
        };
        var operation = Operation("Web");
        var metadata = new List<object>
        {
            new TagsAttribute("Keep-Me"),
            new AutoTagOverride("Review-Tag")
        };

        Apply(options, operation, metadata, "/Sales-Orders/items");

        ShouldHaveTags(operation, "Keep-Me", automaticTag);
    }

    [Fact]
    public void route_tag_formatting_leaves_explicit_spelling_unchanged()
    {
        var options = new DocumentOptions
        {
            TagCase = TagCase.LowerCase,
            TagStripSymbols = true
        };
        var operation = new OpenApiOperation();

        Apply(options, operation, [new TagsAttribute("Keep-Me")], "/Sales-Orders/items");

        ShouldHaveTags(operation, "Keep-Me", "salesorders");
    }

    [Fact]
    public void framework_fallback_tags_are_removed_when_auto_tagging_is_disabled()
    {
        var operation = Operation("Web");

        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, []);

        ShouldHaveTags(operation);
    }

    [Fact]
    public void framework_fallback_tags_are_removed_while_explicit_tags_are_restored()
    {
        var operation = Operation("Web", "Last");

        Apply(new DocumentOptions { AutoTagPathSegmentIndex = 0 }, operation, GroupMetadata());

        ShouldHaveTags(operation, "First", "Middle", "Last");
    }

    static void Apply(DocumentOptions options, OpenApiOperation operation, IList<object> metadata, string bareRoute = "/first/middle/last", bool dontAutoTag = false)
    {
        var definition = new EndpointDefinition(typeof(TagEndpoint), typeof(EmptyRequest), typeof(object));

        if (dontAutoTag)
            definition.DontAutoTag();

        new OperationMetadataTransformer(options, new SharedContext()).ApplyAutoTag(operation, definition, bareRoute, metadata);
    }

    static List<object> GroupMetadata()
        =>
        [
            new TagsAttribute("First"),
            new TagsAttribute("Middle"),
            new TagsAttribute("Last")
        ];

    static OpenApiOperation Operation(params string[] tags)
        => new() { Tags = new HashSet<OpenApiTagReference>(tags.Select(static tag => new OpenApiTagReference(tag))) };

    static void ShouldHaveTags(OpenApiOperation operation, params string[] expected)
    {
        var actual = operation.Tags?.Select(static tag => tag.Name).ToArray() ?? [];

        actual.ShouldAllBe(static name => name != null);
        actual.Distinct(StringComparer.Ordinal).Count().ShouldBe(actual.Length);
        actual.OrderBy(static name => name, StringComparer.Ordinal)
              .ShouldBe(expected.OrderBy(static name => name, StringComparer.Ordinal).ToArray());
    }

    sealed class TagEndpoint : EndpointWithoutRequest
    {
        public override void Configure() { }
    }
}
