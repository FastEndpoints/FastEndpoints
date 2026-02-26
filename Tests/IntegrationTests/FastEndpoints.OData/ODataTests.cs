using System.Net;
using Sample.Model;

namespace Int.OData;

public class ODataTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task GetMetadata()
    {
        var rsp = await App.Client.GetAsync("/odata/$metadata");

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await rsp.Content.ReadAsStringAsync(Cancellation);
        content.ShouldContain("Edmx");
        content.ShouldContain("DataServices");
        content.ShouldContain("Customer");
    }

    [Fact]
    public async Task GetCustomersOrderedByName()
    {
        var (rsp, res) = await App.Client.SENDAsync<EmptyRequest, Customer[]>(HttpMethod.Get, "/odata/customers?orderby=Name asc&top=3", EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldNotBeNull();
        res.Length.ShouldBe(3);
        res[0].Name.ShouldBe("Emily Williams");
        res[1].Name.ShouldBe("Jane Smith");
        res[2].Name.ShouldBe("John Doe");
    }

    [Fact]
    public async Task GetCustomersFilteredByNamePrefix()
    {
        var (rsp, res) = await App.Client.SENDAsync<EmptyRequest, Customer[]>(
                             HttpMethod.Get,
                             "/odata/customers?$filter=startswith(Name,'Michael')&orderby=Name asc&top=3",
                             EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldNotBeNull();
        res.Length.ShouldBe(1);
        res[0].Name.ShouldBe("Michael Johnson");
    }

    [Fact]
    public async Task GetCustomerByExactName()
    {
        var (rsp, res) = await App.Client.SENDAsync<EmptyRequest, Customer[]>(HttpMethod.Get, "/odata/customers?$filter=Name eq 'John Doe'", EmptyRequest.Instance);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldNotBeNull();
        res.Length.ShouldBe(1);
        res[0].Name.ShouldBe("John Doe");
        res[0].Address.City.ShouldBe("New York");
    }
}