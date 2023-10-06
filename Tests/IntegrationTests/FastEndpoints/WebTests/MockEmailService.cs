using Web.Services;

namespace Web;

class MockEmailService : IEmailService
{
    public bool IsDisposed { get; private set; }
    public string SendEmail() => "Email was not sent during testing!";
    public void Dispose() => IsDisposed = true;
}