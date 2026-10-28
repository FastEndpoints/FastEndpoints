using System.Reflection;
using FastEndpoints;
using FastEndpoints.Swagger;
using NSwag;
using Shouldly;
using Xunit;

namespace Swagger;

public class QueryParameterVisibilityTests
{
    [Theory]
    [InlineData(nameof(Request.OptionalClaim), true)]
    [InlineData(nameof(Request.HiddenClaim), false)]
    [InlineData(nameof(Request.RequiredClaim), false)]
    [InlineData(nameof(Request.HiddenRequiredClaim), false)]
    [InlineData(nameof(Request.OptionalPermission), true)]
    [InlineData(nameof(Request.HiddenPermission), false)]
    [InlineData(nameof(Request.RequiredPermission), false)]
    [InlineData(nameof(Request.HiddenRequiredPermission), false)]
    [InlineData(nameof(Request.HiddenClaimFallback), false)]
    [InlineData(nameof(Request.HiddenPermissionFallback), false)]
    [InlineData(nameof(Request.Search), true)]
    public void Claim_And_Permission_Query_Parameters_Respect_Visibility(string propertyName, bool expected)
    {
        var method = typeof(OperationProcessor).GetMethod("ShouldAddQueryParam", BindingFlags.Static | BindingFlags.NonPublic)!;
        var property = typeof(Request).GetProperty(propertyName)!;

        foreach (var isBodylessRequest in new[] { true, false })
        {
            var result = (bool)method.Invoke(null, [property, new List<OpenApiParameter>(), isBodylessRequest, new DocumentOptions(null!)])!;
            result.ShouldBe(expected);
        }
    }

    sealed class Request
    {
        [FromClaim("tenant-id", isRequired: false), QueryParam]
        public string? OptionalClaim { get; set; }

        [FromClaim(isRequired: false, removeFromSchema: true), QueryParam]
        public string? HiddenClaim { get; set; }

        [FromClaim, QueryParam]
        public string? RequiredClaim { get; set; }

        [FromClaim(removeFromSchema: true), QueryParam]
        public string? HiddenRequiredClaim { get; set; }

        [HasPermission("edit", isRequired: false), QueryParam]
        public bool OptionalPermission { get; set; }

        [HasPermission("edit", isRequired: false, removeFromSchema: true), QueryParam]
        public bool HiddenPermission { get; set; }

        [HasPermission("edit"), QueryParam]
        public bool RequiredPermission { get; set; }

        [HasPermission("edit", removeFromSchema: true), QueryParam]
        public bool HiddenRequiredPermission { get; set; }

        [FromClaim("tenant-id", isRequired: false, RemoveFromSchema = true)]
        public string? HiddenClaimFallback { get; set; }

        [HasPermission("edit", isRequired: false, RemoveFromSchema = true)]
        public bool HiddenPermissionFallback { get; set; }

        [QueryParam]
        public string? Search { get; set; }
    }
}
