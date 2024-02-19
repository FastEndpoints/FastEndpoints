using Web.Services;

namespace FixtureTests;

public class FixtureBTests : TestClass<FixtureB>
{
    public FixtureBTests(FixtureB f, ITestOutputHelper o) : base(f, o) { }

    //[Fact]
    public async Task Actual_Email_Service_Is_Resolved()
    {
        var svc = Config.ServiceResolver.Resolve<IEmailService>();
        svc.SendEmail().Should().Be("Email actually sent!");
    }
}

//TODO: currently it's not supported to resolve different types of mock services for the same interface from more than 1 fixture.