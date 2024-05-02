using Web.Services;

namespace FixtureTests;

public class FixtureBTests(FixtureB sut) : TestBase<FixtureB>
{
    [Fact]
    public async Task Actual_Email_Service_Is_Resolved()
    {
        var svc = sut.Services.GetRequiredService<IEmailService>();
        svc.SendEmail().Should().Be("Email actually sent!");
    }
}