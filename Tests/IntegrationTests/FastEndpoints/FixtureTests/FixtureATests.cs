using Web.Services;

namespace FixtureTests;

public class FixtureATests : TestClass<FixtureA>
{
    public FixtureATests(FixtureA f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task Mock_Email_Service_Is_Resolved()
    {
        var svc = Config.ServiceResolver.Resolve<IEmailService>();
        svc.SendEmail().Should().Be("Email was not sent during testing!");
    }
}