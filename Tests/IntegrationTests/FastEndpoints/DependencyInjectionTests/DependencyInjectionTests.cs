namespace DependencyInjection;

public class DiTests(AppFixture App) : TestBase<AppFixture>
{
    [Fact]
    public async Task Service_Registration_Generator()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.ServiceRegistrationGeneratorTest.Endpoint, string[]>();

        rsp.IsSuccessStatusCode.Should().BeTrue();

        res.Should().Equal("Scoped", "Transient", "Singleton");
    }

    [Fact]
    public async Task Keyed_Service_Property_Injection()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.KeyedServicesTests.Endpoint, string>();

        rsp.IsSuccessStatusCode.Should().BeTrue();

        res.Should().Be("AAA");
    }
}