namespace Web.Services;

public interface IEmailService
{
    string SendEmail();
}

public class EmailService : IEmailService
{
    public string SendEmail() => "Email actually sent!";
}