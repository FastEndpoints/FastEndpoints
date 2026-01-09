using FastEndpoints;
using Microsoft.AspNetCore.Http;
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

}