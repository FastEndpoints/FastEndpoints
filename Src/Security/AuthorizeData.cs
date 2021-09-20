using Microsoft.AspNetCore.Authorization;

namespace EZEndpoints
{
    public record AuthorizeData : IAuthorizeData
    {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }
    }
}
