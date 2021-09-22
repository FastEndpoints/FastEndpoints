namespace Admin.Login
{
    public class Response
    {
        public string? JWTToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string[]? Permissions { get; set; }
    }
}
