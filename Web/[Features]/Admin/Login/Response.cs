namespace Admin.Login
{
    public class Response
    {
        public string? JWTToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public IEnumerable<string>? Permissions { get; set; }
    }
}
