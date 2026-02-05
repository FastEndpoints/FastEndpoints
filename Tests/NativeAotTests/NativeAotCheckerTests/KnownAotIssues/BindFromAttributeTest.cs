using System.Net.Http.Json;
using NativeAotChecker.Endpoints.KnownAotIssues;

namespace NativeAotCheckerTests;

public class BindFromAttributeTest(App app)
{
    /// <summary>
    /// Tests [BindFrom] attribute for property name aliasing in AOT mode.
    /// AOT ISSUE: [BindFrom("alias")] requires attribute reflection to discover alias names.
    /// Property-to-JSON-property mapping uses GetCustomAttribute<BindFromAttribute>().
    /// This binding source aliasing is entirely reflection-based and fails in AOT.
    /// </summary>
    [Fact] // AOT ISSUE: BindFrom attribute for property name aliasing may not work in Native AOT mode
    public async Task BindFrom_Attribute_Works_In_AOT_Mode()
    {
        var req = new BindFromRequest
        {
            CustomerId = 123,
            ProductName = "Test Product",
            Quantity = 5,
            Category = "electronics"
        };

        // Send with aliased JSON property names
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                customer_id = 123,
                product_name = "Test Product",
                qty = 5
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("bind-from-test?cat=electronics", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<BindFromResponse>();

        res.ShouldNotBeNull();
        res.CustomerId.ShouldBe(123);
        res.ProductName.ShouldBe("Test Product");
        res.Quantity.ShouldBe(5);
        res.Category.ShouldBe("electronics");
        res.AllBindingsWorked.ShouldBeTrue();
    }
}
