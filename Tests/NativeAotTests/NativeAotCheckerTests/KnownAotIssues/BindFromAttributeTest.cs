using System.Net.Http.Json;
using System.Text.Json;
using NativeAotChecker.Endpoints.KnownAotIssues;

namespace NativeAotCheckerTests;

public class BindFromAttributeTest(App app)
{
    [Fact]
    public async Task BindFrom_Attribute_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            JsonSerializer.Serialize(
                new
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

        var res = await rsp.Content.ReadFromJsonAsync<BindFromResponse>(
                      new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower // this was missing
                      });

        res.ShouldNotBeNull();
        res.CustomerId.ShouldBe(123);
        res.ProductName.ShouldBe("Test Product");
        res.Quantity.ShouldBe(5);
        res.Category.ShouldBe("electronics");
        res.AllBindingsWorked.ShouldBeTrue();
    }
}