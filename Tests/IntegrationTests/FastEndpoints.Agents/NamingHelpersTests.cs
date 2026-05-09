namespace FastEndpoints.Agents.Tests;

public class NamingHelpersTests
{
    // ── PascalCase (endpoint type names) ───────────────────────────────────
    [Theory]
    [InlineData("AddProduct", "add_product")]
    [InlineData("GetOrderById", "get_order_by_id")]
    [InlineData("GetOrderByID", "get_order_by_id")]        // underscore before the all-upper run
    [InlineData("ListProductsEndpoint", "list_products_endpoint")]
    [InlineData("ABCThing", "abcthing")]                   // all-upper prefix stays joined (no lower→upper boundary)
    public void ToSnakeCase_handles_pascal_case(string input, string expected)
        => NamingHelpers.ToSnakeCase(input).ShouldBe(expected);

    // ── Title-case / space-separated (OpenAPI Summary values) ─────────────
    [Theory]
    [InlineData("Add Product", "add_product")]
    [InlineData("Adds a new Product to a Product Group", "adds_a_new_product_to_a_product_group")]
    [InlineData("Get Order By ID", "get_order_by_id")]
    [InlineData("  leading spaces  ", "leading_spaces")]   // leading/trailing spaces stripped
    [InlineData("double  space", "double_space")]           // consecutive spaces → single _
    public void ToSnakeCase_handles_spaces(string input, string expected)
        => NamingHelpers.ToSnakeCase(input).ShouldBe(expected);

    // ── Hyphen-separated ───────────────────────────────────────────────────
    [Theory]
    [InlineData("get-order", "get_order")]
    [InlineData("add-new-product", "add_new_product")]
    public void ToSnakeCase_handles_hyphens(string input, string expected)
        => NamingHelpers.ToSnakeCase(input).ShouldBe(expected);

    // ── Already snake_case or lowercase ───────────────────────────────────
    [Theory]
    [InlineData("add_product", "add_product")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("lowercase", "lowercase")]
    public void ToSnakeCase_leaves_snake_case_unchanged(string input, string expected)
        => NamingHelpers.ToSnakeCase(input).ShouldBe(expected);

    // ── Edge cases ─────────────────────────────────────────────────────────
    [Fact]
    public void ToSnakeCase_returns_empty_string_unchanged()
        => NamingHelpers.ToSnakeCase("").ShouldBe("");

    [Fact]
    public void ToSnakeCase_single_word_lowercases()
        => NamingHelpers.ToSnakeCase("Orders").ShouldBe("orders");
}
