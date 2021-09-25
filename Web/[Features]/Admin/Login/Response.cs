using FastEndpoints;

namespace Admin.Login
{
    public class Response : IResponse
    {
        public string? JWTToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public IEnumerable<string>? Permissions { get; set; }
    }
}
