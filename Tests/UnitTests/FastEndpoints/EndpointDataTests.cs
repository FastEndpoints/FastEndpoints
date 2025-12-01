using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EPData;

public class EndpointDataTests
{
    [Fact]
    public void ConfigureIsExecuted()
    {
        var ep = Factory.Create<ConfigureEndpoint>();
        ep.Definition.Routes.ShouldHaveSingleItem("configure/test");
    }

    [Fact]
    public void ItCanFilterTypes()
    {
        const string typename = "Foo";
        var options = new EndpointDiscoveryOptions
        {
            Filter = t => t.Name.Contains(typename, StringComparison.OrdinalIgnoreCase),
            Assemblies = [GetType().Assembly]
        };

        var sut = new EndpointData(options, new());
        var ep = new Foo
        {
            Definition = sut.Found[0]
        };
        sut.Found[0].Initialize(ep, null);

        sut.Found.Length.ShouldBe(1);
        sut.Found[0].Routes.Count().ShouldBe(1);
        sut?.Found[0]?.Routes?[0].ShouldBeEquivalentTo(typename);
    }

    static EndpointDefinition WireUpProcessorEndpoint()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton<IEndpointFactory, EndpointFactory>();
        var sp = services.BuildServiceProvider();
        ServiceResolver.Instance = sp.GetRequiredService<IServiceResolver>();
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
    public void BaselineProcessorOrder()
    {
        var epDef = WireUpProcessorEndpoint();

        // Simulate global definition
        epDef.PreProcessors(Order.Before, new ProcOne(), new ProcTwo());
        epDef.PreProcessors(Order.After, new ProcThree(), new ProcFour());
        epDef.PostProcessors(Order.Before, new PostProcOne(), new PostProcTwo());
        epDef.PostProcessors(Order.After, new PostProcThree(), new PostProcFour());

        epDef.PreProcessorList.Count.ShouldBe(5);
        epDef.PreProcessorList[0].ShouldBeOfType<ProcOne>();
        epDef.PreProcessorList[1].ShouldBeOfType<ProcTwo>();
        epDef.PreProcessorList[2].ShouldBeOfType<ProcRequest>();
        epDef.PreProcessorList[3].ShouldBeOfType<ProcThree>();
        epDef.PreProcessorList[4].ShouldBeOfType<ProcFour>();

        epDef.PostProcessorList.Count.ShouldBe(5);
        epDef.PostProcessorList[0].ShouldBeOfType<PostProcOne>();
        epDef.PostProcessorList[1].ShouldBeOfType<PostProcTwo>();
        epDef.PostProcessorList[2].ShouldBeOfType<PostProcRequest>();
        epDef.PostProcessorList[3].ShouldBeOfType<PostProcThree>();
        epDef.PostProcessorList[4].ShouldBeOfType<PostProcFour>();
    }

    [Fact]
    public void MultiCallProcessorOrder()
    {
        var epDef = WireUpProcessorEndpoint();

        // Simulate global definition
        epDef.PreProcessors(Order.Before, new ProcOne());
        epDef.PreProcessors(Order.Before, new ProcTwo());
        epDef.PreProcessors(Order.After, new ProcThree());
        epDef.PreProcessors(Order.After, new ProcFour());
        epDef.PostProcessors(Order.Before, new PostProcOne());
        epDef.PostProcessors(Order.Before, new PostProcTwo());
        epDef.PostProcessors(Order.After, new PostProcThree());
        epDef.PostProcessors(Order.After, new PostProcFour());

        epDef.PreProcessorList.Count.ShouldBe(5);
        epDef.PreProcessorList[0].ShouldBeOfType<ProcOne>();
        epDef.PreProcessorList[1].ShouldBeOfType<ProcTwo>();
        epDef.PreProcessorList[2].ShouldBeOfType<ProcRequest>();
        epDef.PreProcessorList[3].ShouldBeOfType<ProcThree>();
        epDef.PreProcessorList[4].ShouldBeOfType<ProcFour>();
        epDef.PostProcessorList.Count.ShouldBe(5);
        epDef.PostProcessorList[0].ShouldBeOfType<PostProcOne>();
        epDef.PostProcessorList[1].ShouldBeOfType<PostProcTwo>();
        epDef.PostProcessorList[2].ShouldBeOfType<PostProcRequest>();
        epDef.PostProcessorList[3].ShouldBeOfType<PostProcThree>();
        epDef.PostProcessorList[4].ShouldBeOfType<PostProcFour>();
    }

    [Fact]
    public void ServiceResolvedProcessorOrder()
    {
        var epDef = WireUpProcessorEndpoint();

        // Simulate global definition
        epDef.PreProcessor<ProcOne>(Order.Before);
        epDef.PreProcessor<ProcTwo>(Order.Before);
        epDef.PreProcessor<ProcThree>(Order.After);
        epDef.PreProcessor<ProcFour>(Order.After);
        epDef.PostProcessor<PostProcOne>(Order.Before);
        epDef.PostProcessor<PostProcTwo>(Order.Before);
        epDef.PostProcessor<PostProcThree>(Order.After);
        epDef.PostProcessor<PostProcFour>(Order.After);

        epDef.PreProcessorList.Count.ShouldBe(5);
        epDef.PreProcessorList[0].ShouldBeOfType<ProcOne>();
        epDef.PreProcessorList[1].ShouldBeOfType<ProcTwo>();
        epDef.PreProcessorList[2].ShouldBeOfType<ProcRequest>();
        epDef.PreProcessorList[3].ShouldBeOfType<ProcThree>();
        epDef.PreProcessorList[4].ShouldBeOfType<ProcFour>();
        epDef.PostProcessorList.Count.ShouldBe(5);
        epDef.PostProcessorList[0].ShouldBeOfType<PostProcOne>();
        epDef.PostProcessorList[1].ShouldBeOfType<PostProcTwo>();
        epDef.PostProcessorList[2].ShouldBeOfType<PostProcRequest>();
        epDef.PostProcessorList[3].ShouldBeOfType<PostProcThree>();
        epDef.PostProcessorList[4].ShouldBeOfType<PostProcFour>();
    }
}

public class Foo : EndpointWithoutRequest
{
    public override void Configure()
        => Get(nameof(Foo));

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(ct);
}

public class Boo : EndpointWithoutRequest
{
    public override void Configure()
        => Get(nameof(Boo));

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(ct);
}

public class PreProcessorRegistration : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get(nameof(PreProcessorRegistration));
        PreProcessors(new ProcRequest());
        PostProcessors(new PostProcRequest());
    }
}

public class ProcOne : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        => throw new NotImplementedException();
}

public class PostProcOne : IGlobalPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
        => throw new NotImplementedException();
}

public class ProcTwo : ProcOne { }

public class PostProcTwo : PostProcOne { }

public class ProcThree : ProcOne { }

public class PostProcThree : PostProcOne { }

public class ProcFour : ProcOne { }

public class PostProcFour : PostProcOne { }

public class ProcRequest : IPreProcessor<EmptyRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<EmptyRequest> context, CancellationToken ct)
        => throw new NotImplementedException();
}

public class PostProcRequest : IPostProcessor<EmptyRequest, object?>
{
    public Task PostProcessAsync(IPostProcessorContext<EmptyRequest, object?> context, CancellationToken ct)
        => throw new NotImplementedException();
}

public class ConfigureEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("configure/test");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}