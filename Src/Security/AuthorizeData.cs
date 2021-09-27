using Microsoft.AspNetCore.Authorization;

namespace FastEndpoints.Security
{
    internal record AuthorizeData : IAuthorizeData
    {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }
    }
}
