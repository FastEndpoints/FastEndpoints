using FastEndpoints;

namespace Admin.Login
{
    public class Request : IRequest
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }
}