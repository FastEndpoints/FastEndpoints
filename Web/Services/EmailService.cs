namespace Web.Services;

public interface IEmailService : IDisposable
{
    public bool IsDisposed { get; }
    string SendEmail();
}

public class EmailService : IEmailService
{
    public bool IsDisposed { get; private set; }
    public string SendEmail() => !IsDisposed ? "Email actually sent!" : throw new Exception("The service has been disposed before!.");
    public void Dispose() => IsDisposed = true;
}