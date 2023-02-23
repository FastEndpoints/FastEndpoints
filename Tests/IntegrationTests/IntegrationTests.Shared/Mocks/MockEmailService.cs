using Web.Services;

namespace IntegrationTests.Shared.Mocks;

internal class MockEmailService : IEmailService
{
    public bool IsDisposed { get; private set; }
    public string SendEmail() => "Email was not sent during testing!";
    public void Dispose() => IsDisposed = true;
}