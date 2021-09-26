using Web.Services;

namespace Test
{
    internal class MockEmailService : IEmailService
    {
        public string SendEmail() => "Email was not sent during testing!";
    }
}
