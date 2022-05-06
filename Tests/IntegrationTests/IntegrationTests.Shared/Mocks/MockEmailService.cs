using Web.Services;

namespace IntegrationTests.Shared.Mocks;

internal class MockEmailService : IEmailService
{
    public string SendEmail() => "Email was not sent during testing!";
}