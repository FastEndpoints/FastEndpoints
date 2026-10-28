extern alias A2AAsm;
using System.Text.Json;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using A2AAsm::FastEndpoints.A2A;

namespace FastEndpoints.Agents.Tests;

public class AgentEndpointMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pre_processors_can_read_custom_metadata_in_registration_order(bool a2a)
    {
        using var provider = BuildServices<MetadataEndpoint>(a2a);
        var definition = provider.GetRequiredService<EndpointData>().Found.Single();

        await Invoke(provider, definition, a2a);

        var metadata = provider.GetRequiredService<MetadataProbe>().Metadata.ShouldNotBeNull();
        metadata.GetMetadata<EndpointDefinition>().ShouldBeSameAs(definition);
        object[] expected = [definition, ..definition.EndpointMetadata!];
        metadata.ToArray().ShouldBe(expected);
        metadata.GetOrderedMetadata<Declaration>().Select(d => d.Name).ShouldBe(new[] { "first", "last" });
        metadata.GetMetadata<Declaration>().ShouldBeSameAs(definition.EndpointMetadata![2]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Endpoints_without_custom_metadata_still_expose_the_definition(bool a2a, bool explicitEmpty)
    {
        using var provider = BuildServices<NoMetadataEndpoint>(a2a);
        var definition = provider.GetRequiredService<EndpointData>().Found.Single();
        if (explicitEmpty)
            definition.Metadata([]);

        await Invoke(provider, definition, a2a);

        var metadata = provider.GetRequiredService<MetadataProbe>().Metadata.ShouldNotBeNull();
        metadata.ToArray().ShouldBe(new object[] { definition });
        metadata.GetOrderedMetadata<Declaration>().ShouldBeEmpty();
    }

    static ServiceProvider BuildServices<TEndpoint>(bool a2a) where TEndpoint : BaseEndpoint
    {
        Factory.RegisterTestServices(s => s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<MetadataProbe>();
        services.AddFastEndpoints([typeof(TEndpoint)]);
        if (a2a)
            services.AddA2A();
        else
            services.AddMcp();

        var provider = services.BuildServiceProvider();
        var definition = provider.GetRequiredService<EndpointData>().Found.Single();
        definition.Initialize(ActivatorUtilities.CreateInstance<TEndpoint>(provider), new DefaultHttpContext { RequestServices = provider });

        return provider;
    }

    static async Task Invoke(IServiceProvider provider, EndpointDefinition definition, bool a2a)
    {
        var args = JsonSerializer.SerializeToElement(new { });
        if (a2a)
        {
            var result = await provider.GetRequiredService<A2AAsm::FastEndpoints.Agents.EndpointInvoker>()
                                       .InvokeAsync(definition, args, null, default);
            result.Status.ShouldBe(A2AAsm::FastEndpoints.Agents.InvocationStatus.Success);
        }
        else
        {
            var result = await provider.GetRequiredService<EndpointInvoker>().InvokeAsync(definition, args, null, default);
            result.Status.ShouldBe(InvocationStatus.Success);
        }
    }

    sealed record Declaration(string Name);

    sealed class MetadataProbe
    {
        public EndpointMetadataCollection? Metadata { get; set; }
    }

    sealed class MetadataReader : IGlobalPreProcessor
    {
        public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        {
            context.HttpContext.RequestServices.GetRequiredService<MetadataProbe>().Metadata = context.HttpContext.GetEndpoint()?.Metadata;

            return Task.CompletedTask;
        }
    }

    sealed class MetadataEndpoint : EndpointWithoutRequest<string>
    {
        public override void Configure()
        {
            Post("/agent-metadata/custom");
            Definition.Metadata(new Declaration("first"), new object());
            Definition.Metadata(new Declaration("last"));
            Definition.PreProcessors(Order.Before, new MetadataReader());
        }

        public override Task HandleAsync(CancellationToken ct)
            => Send.OkAsync("ok", ct);
    }

    sealed class NoMetadataEndpoint : EndpointWithoutRequest<string>
    {
        public override void Configure()
        {
            Post("/agent-metadata/none");
            Definition.PreProcessors(Order.Before, new MetadataReader());
        }

        public override Task HandleAsync(CancellationToken ct)
            => Send.OkAsync("ok", ct);
    }
}
