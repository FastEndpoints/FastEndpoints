using FastEndpoints;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace RequestBinder;

public class RequestBinderTests
{
    [Fact]
    public async Task CanBindClassDto()
    {
        var hCtx = new DefaultHttpContext();
        var intgr = 123;
        hCtx.Request.RouteValues["Int"] = intgr;
        var guid = Guid.Empty;
        hCtx.Request.RouteValues["Guid"] = guid;
        hCtx.Request.RouteValues["req_quired"] = false;
        var binder = new RequestBinder<RequestClass>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<RequestClass>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.Int.ShouldBe(intgr);
        res.Guid.ShouldBe(guid);
        res.DefaultValueProp.ShouldBe(345);
        res.OptionalProp.ShouldBe(678);
        res.Required.ShouldBeFalse();
    }

    [Fact]
    public async Task CanBindClassDto_WithDefaultStructParameter() {
        var hCtx = new DefaultHttpContext();
        var binder = new RequestBinder<RequestClassWithDefaultStructParameter>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<RequestClassWithDefaultStructParameter>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        // Should be a zeroed struct - not a default constructor call
        res.SomeStruct.Value.ShouldBe(0);
        res.SomeStruct.ConstructorCalled.ShouldBe(false);
    }

    [Fact]
    public async Task MissingInputThrowsFailure()
    {
        var hCtx = new DefaultHttpContext();
        var binder = new RequestBinder<RequestClass>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<RequestClass>)binder).RequiredProps);

        var ex = Should.Throw<ValidationFailureException>(async () => await binder.BindAsync(ctx, default));
        ex.Failures!.ShouldContain(f => f.PropertyName == "req_quired");
    }

    [Fact]
    public async Task HasPermissionBindsWithExactClaimValueCasing()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("permissions", "Admin")]))
        };
        var binder = new RequestBinder<PermissionRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<PermissionRequest>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.HasAdminPermission.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermissionDoesNotBindWithDifferentClaimValueCasing()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("permissions", "Admin")]))
        };
        var binder = new RequestBinder<OptionalPermissionRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<OptionalPermissionRequest>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.HasAdminPermission.ShouldBeFalse();
    }

    [Fact]
    public async Task RequiredHasPermissionFailsWithDifferentClaimValueCasing()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("permissions", "Admin")]))
        };
        var binder = new RequestBinder<RequiredPermissionRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<RequiredPermissionRequest>)binder).RequiredProps);

        var ex = Should.Throw<ValidationFailureException>(async () => await binder.BindAsync(ctx, default));
        ex.Failures!.ShouldContain(f => f.PropertyName == "admin");
    }

    [Fact]
    public async Task FromClaimBindsSingleValue()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("dept", "sales"), new("unrelated-claim", "xyz")]))
        };
        var binder = new RequestBinder<ClaimRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<ClaimRequest>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.Department.ShouldBe("sales");
    }

    [Fact]
    public async Task FromClaimBindsMultipleValuesToCollectionProp()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("role", "admin"), new("role", "manager")]))
        };
        var binder = new RequestBinder<ClaimRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<ClaimRequest>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.Roles.ShouldBe(["admin", "manager"]);
    }

    [Fact]
    public async Task FromClaimBindsTwoPropsFromSameClaimType()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([new("dept", "sales")]))
        };
        var binder = new RequestBinder<DuplicateClaimTypeRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<DuplicateClaimTypeRequest>)binder).RequiredProps);

        var res = await binder.BindAsync(ctx, default);

        res.DepartmentA.ShouldBe("sales");
        res.DepartmentB.ShouldBe("sales");
    }

    [Fact]
    public async Task RequiredFromClaimFailsWhenMissing()
    {
        var hCtx = new DefaultHttpContext
        {
            User = new(new ClaimsIdentity([]))
        };
        var binder = new RequestBinder<RequiredClaimRequest>();
        var ctx = new BinderContext(hCtx, [], null, false, ((IRequestBinder<RequiredClaimRequest>)binder).RequiredProps);

        var ex = Should.Throw<ValidationFailureException>(async () => await binder.BindAsync(ctx, default));
        ex.Failures!.ShouldContain(f => f.PropertyName == "dept");
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class RequestClass(int intgr, int optionalProp = 678)
    {
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public int Int { get; set; } = intgr;
        public Guid Guid { get; set; }
        public int DefaultValueProp => 345;
        public int OptionalProp { get; } = optionalProp;

        [RouteParam(IsRequired = true), BindFrom("req_quired")]
        public bool Required { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class RequestClassWithDefaultStructParameter(SomeStruct someOptionalStruct = default) {

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public SomeStruct SomeStruct { get; set; } = someOptionalStruct;

    }

    struct SomeStruct {

        public int Value;
        public bool ConstructorCalled;

        // ReSharper disable once UnusedMember.Local
        public SomeStruct(int value) {
            Value = value;
            ConstructorCalled = true;
        }

    }

    sealed class PermissionRequest
    {
        [HasPermission("Admin", isRequired: false)]
        public bool HasAdminPermission { get; set; }
    }

    sealed class OptionalPermissionRequest
    {
        [HasPermission("admin", isRequired: false)]
        public bool HasAdminPermission { get; set; }
    }

    sealed class RequiredPermissionRequest
    {
        [HasPermission("admin")]
        public bool HasAdminPermission { get; set; }
    }

    sealed class ClaimRequest
    {
        [FromClaim("dept", isRequired: false)]
        public string? Department { get; set; }

        [FromClaim("role", isRequired: false)]
        public List<string>? Roles { get; set; }
    }

    sealed class DuplicateClaimTypeRequest
    {
        [FromClaim("dept", isRequired: false)]
        public string? DepartmentA { get; set; }

        [FromClaim("dept", isRequired: false)]
        public string? DepartmentB { get; set; }
    }

    sealed class RequiredClaimRequest
    {
        [FromClaim("dept")]
        public string? Department { get; set; }
    }

}
