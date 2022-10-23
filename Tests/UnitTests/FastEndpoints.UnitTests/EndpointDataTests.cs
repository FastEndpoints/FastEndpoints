using Xunit;

namespace FastEndpoints.UnitTests;
public class EndpointDataTests
{
    [Fact]
    public void ItCanFilterTypes()
    {
        const string typename = "foo";
        var options = new EndpointDiscoveryOptions
        {
            Filter = t => t.Name.Contains(typename, StringComparison.OrdinalIgnoreCase),
            Assemblies = new[] { GetType().Assembly }
        };

        var sut = new EndpointData(options);
        var ep = new Foo
        {
            Definition = sut.Found[0]
        };
        sut.Found[0].Initialize(ep, null);

        sut.Found.Should().HaveCount(1);
        sut.Found[0].Routes.Should().HaveCount(1);
        sut?.Found[0]?.Routes?[0].Should().BeEquivalentTo(typename);
    }
}

public class Foo : EndpointWithoutRequest
{
    public override void Configure() => Get(nameof(Foo));
    public async override Task HandleAsync(CancellationToken ct) => await SendOkAsync(ct);
}

public class Boo : EndpointWithoutRequest
{
    public override void Configure() => Get(nameof(Boo));
    public async override Task HandleAsync(CancellationToken ct) => await SendOkAsync(ct);
}
