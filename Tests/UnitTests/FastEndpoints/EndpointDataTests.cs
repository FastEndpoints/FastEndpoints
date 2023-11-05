using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EPData;

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

        var sut = new EndpointData(options, new());
        var ep = new Foo
        {
            Definition = sut.Found[0]
        };
        sut.Found[0].Initialize(ep, null);

        sut.Found.Should().HaveCount(1);
        sut.Found[0].Routes.Should().HaveCount(1);
        sut?.Found[0]?.Routes?[0].Should().BeEquivalentTo(typename);
    }

    EndpointDefinition WireupPreProcessorEndpoint()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton<IEndpointFactory, EndpointFactory>();
        var sp = services.BuildServiceProvider();
        Config.ServiceResolver = sp.GetRequiredService<IServiceResolver>();
        var epFactory = sp.GetRequiredService<IEndpointFactory>();
        using var scope = sp.CreateScope();
        var httpCtx = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var epDef = new EndpointDefinition(typeof(PreProcessorRegistration), typeof(EmptyRequest), typeof(EmptyResponse));
        var baseEp = epFactory.Create(epDef, httpCtx);
        epDef.ImplementsConfigure = true; // Override as there's no EndpointData resolver
        epDef.Initialize(baseEp, httpCtx);

        return epDef;
    }
    
    [Fact]
    public void BaselinePreProcessorOrder()
    {
        var epDef = WireupPreProcessorEndpoint();

        // Simulate global definition
        epDef.PreProcessors(Order.Before, new ProcOne(), new ProcTwo());
        epDef.PreProcessors(Order.After, new ProcThree(), new ProcFour());

        epDef.PreProcessorList.Should().HaveCount(5);
        epDef.PreProcessorList[0].Should().BeOfType<ProcOne>();
        epDef.PreProcessorList[1].Should().BeOfType<ProcTwo>();
        epDef.PreProcessorList[2].Should().BeOfType<ProcRequest>();
        epDef.PreProcessorList[3].Should().BeOfType<ProcThree>();
        epDef.PreProcessorList[4].Should().BeOfType<ProcFour>();
    }

    [Fact]
    public void MultiCallPreProcessorOrder()
    {
        var epDef = WireupPreProcessorEndpoint();

        // Simulate global definition
        epDef.PreProcessors(Order.Before, new ProcOne());
        epDef.PreProcessors(Order.Before, new ProcTwo());
        epDef.PreProcessors(Order.After, new ProcThree());
        epDef.PreProcessors(Order.After, new ProcFour());

        epDef.PreProcessorList.Should().HaveCount(5);
        epDef.PreProcessorList[0].Should().BeOfType<ProcOne>();
        epDef.PreProcessorList[1].Should().BeOfType<ProcTwo>();
        epDef.PreProcessorList[2].Should().BeOfType<ProcRequest>();
        epDef.PreProcessorList[3].Should().BeOfType<ProcThree>();
        epDef.PreProcessorList[4].Should().BeOfType<ProcFour>();
    }

    [Fact]
    public void ServiceResolvedPreProcessorOrder()
    {
        var epDef = WireupPreProcessorEndpoint();
        // Simulate global definition
        epDef.PreProcessor<ProcOne>(Order.Before);
        epDef.PreProcessor<ProcTwo>(Order.Before);
        epDef.PreProcessor<ProcThree>(Order.After);
        epDef.PreProcessor<ProcFour>(Order.After);

        epDef.PreProcessorList.Should().HaveCount(5);
        epDef.PreProcessorList[0].Should().BeOfType<ProcOne>();
        epDef.PreProcessorList[1].Should().BeOfType<ProcTwo>();
        epDef.PreProcessorList[2].Should().BeOfType<ProcRequest>();
        epDef.PreProcessorList[3].Should().BeOfType<ProcThree>();
        epDef.PreProcessorList[4].Should().BeOfType<ProcFour>();
    }
}

public class Foo : EndpointWithoutRequest
{
    public override void Configure() => Get(nameof(Foo));
    public override async Task HandleAsync(CancellationToken ct) => await SendOkAsync(ct);
}

public class Boo : EndpointWithoutRequest
{
    public override void Configure() => Get(nameof(Boo));
    public override async Task HandleAsync(CancellationToken ct) => await SendOkAsync(ct);
}

public class PreProcessorRegistration : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get(nameof(PreProcessorRegistration));
        PreProcessors(new ProcRequest());
    }
}

public class ProcOne : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        => throw new NotImplementedException();
}

public class ProcTwo : ProcOne { }

public class ProcThree : ProcOne { }

public class ProcFour : ProcOne { }

public class ProcRequest : IPreProcessor<EmptyRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<EmptyRequest> context, CancellationToken ct)
        => throw new NotImplementedException();
}