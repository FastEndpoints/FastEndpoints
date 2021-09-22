using Microsoft.AspNetCore.Authorization;

namespace ApiExpress.Security
{
    public record AuthorizeData : IAuthorizeData
    {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }
    }
}
