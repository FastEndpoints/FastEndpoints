namespace Web.Services;

public interface IEmailService
{
    string SendEmail();
}

public class EmailService : IEmailService, IDisposable
{
    public bool IsDisposed { get; private set; }

    public string SendEmail()
        => !IsDisposed ? "Email actually sent!" : throw new("The service has been disposed before!.");

    public void Dispose()
        => IsDisposed = true;
}