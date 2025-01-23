namespace DependencyInjection;

public class DiTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task Service_Registration_Generator()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.ServiceRegistrationGeneratorTest.Endpoint, string[]>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();

        res.ShouldBe(["Scoped", "Transient", "Singleton"]);
    }

    [Fact]
    public async Task Keyed_Service_Property_Injection()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.KeyedServicesTests.Endpoint, string>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();

        res.ShouldBe("AAA");
    }
}