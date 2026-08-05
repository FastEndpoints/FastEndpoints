using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace Warmup;

[CollectionDefinition(Name, DisableParallelization = true)]
public class WarmupTestCollection
{
    public const string Name = nameof(WarmupTestCollection);
}

[Collection(WarmupTestCollection.Name)]
public class WarmupTests : IDisposable
{
    static readonly SemaphoreSlim _testLock = new(1, 1);

    readonly Func<EndpointDefinition, bool>? _previousEndpointFilter;
    readonly Func<EndpointDefinition, bool>? _previousWarmupFilter;
    readonly bool _previousWarmupRequested;
    readonly JsonSerializerOptions _previousSerializerOptions;
    readonly bool _previousSerializerConfigured;

    public WarmupTests()
    {
        _testLock.Wait();

        _previousEndpointFilter = Config.EpOpts.Filter;
        _previousWarmupFilter = Config.EpOpts.WarmupFilter;
        _previousWarmupRequested = Config.EpOpts.WarmupRequested;
        _previousSerializerOptions = Config.SerOpts.Options;
        _previousSerializerConfigured = MainExtensions.SerializerConfigured;

        ResetState();
    }

    public void Dispose()
    {
        Config.EpOpts.Filter = _previousEndpointFilter;
        Config.EpOpts.WarmupFilter = _previousWarmupFilter;
        Config.EpOpts.WarmupRequested = _previousWarmupRequested;
        Config.SerOpts.Options = _previousSerializerOptions;
        MainExtensions.SerializerConfigured = _previousSerializerConfigured;
        CountingBinder.Reset();
        ResetServiceResolver();
        _testLock.Release();
    }

    static void ResetState()
    {
        Config.EpOpts.Filter = null;
        Config.EpOpts.WarmupFilter = null;
        Config.EpOpts.WarmupRequested = false;
        Config.SerOpts.Options = new();
        MainExtensions.SerializerConfigured = false;
        CountingBinder.Reset();
        ResetServiceResolver();
    }

    [Fact]
    public void Warmup_SetsWarmupRequestedAndFilter()
    {
        var opts = new EndpointOptions();

        static bool Filter(EndpointDefinition _)
            => true;

        opts.WarmupRequested.ShouldBeFalse();
        opts.WarmupFilter.ShouldBeNull();

        opts.Warmup(Filter);

        opts.WarmupRequested.ShouldBeTrue();
        opts.WarmupFilter.ShouldBe(Filter);
    }

    [Fact]
    public async Task Warmup_FilterNull_WarmsAllEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            CountingBinder.InstanceCount.ShouldBe(2);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_FilterReturnsTrueForEndpoint_WarmsOnlyThatEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(def => def.EndpointType == typeof(WarmupEpA)));

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task EndpointFilter_ExcludesEndpoint_SkipsItsWarmup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(
                c =>
                {
                    c.Endpoints.Filter = def => def.EndpointType == typeof(WarmupEpA);
                    c.Endpoints.Warmup();
                });

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_FilterAlwaysFalse_SkipsAllEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(_ => false));

            CountingBinder.InstanceCount.ShouldBe(0);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task UseFastEndpoints_WarmupFilter_WarmsOnlyMatchingEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(def => def.EndpointType == typeof(WarmupEpB)));

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_PrecompilesNestedComplexAndCollectionElementTypeAccessors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupNestedEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(NestedDto), out var nestedDef).ShouldBeTrue();
            nestedDef!.Properties!.TryGetValue(typeof(NestedDto).GetProperty(nameof(NestedDto.City))!, out var cityDef).ShouldBeTrue();
            cityDef!.Getter.ShouldNotBeNull();

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(NestedItemDto), out var itemDef).ShouldBeTrue();
            itemDef!.Properties!.TryGetValue(typeof(NestedItemDto).GetProperty(nameof(NestedItemDto.Qty))!, out var qtyDef).ShouldBeTrue();
            qtyDef!.Getter.ShouldNotBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_DoesNotInstantiateAbstractNestedValidatableType()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupAbstractNestedEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(WarmupAbstractNestedRequest), out var reqDef).ShouldBeTrue();
            reqDef!.Properties!.TryGetValue(
                    typeof(WarmupAbstractNestedRequest).GetProperty(nameof(WarmupAbstractNestedRequest.Child))!,
                    out var childProp)
                .ShouldBeTrue();
            childProp!.Getter.ShouldNotBeNull();

            // Abstract declared type may be cached for IsValidatable, but must not require a factory.
            if (Config.BndOpts.ReflectionCache.TryGetValue(typeof(AbstractNestedDto), out var nestedDef))
                nestedDef!.ObjectFactory.ShouldBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_DoesNotInstantiateNestedTypeWithoutPublicConstructor()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupNoPublicCtorNestedEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(WarmupNoPublicCtorNestedRequest), out var reqDef).ShouldBeTrue();
            reqDef!.Properties!.TryGetValue(
                    typeof(WarmupNoPublicCtorNestedRequest).GetProperty(nameof(WarmupNoPublicCtorNestedRequest.Child))!,
                    out var childProp)
                .ShouldBeTrue();
            childProp!.Getter.ShouldNotBeNull();

            if (Config.BndOpts.ReflectionCache.TryGetValue(typeof(NoPublicCtorNestedDto), out var nestedDef))
                nestedDef!.ObjectFactory.ShouldBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_PrecompilesDerivedCollectionElementTypeAccessors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupDerivedCollectionEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(DerivedCollectionItemDto), out var itemDef).ShouldBeTrue();
            itemDef!.Properties!.TryGetValue(
                    typeof(DerivedCollectionItemDto).GetProperty(nameof(DerivedCollectionItemDto.Label))!,
                    out var labelDef)
                .ShouldBeTrue();
            labelDef!.Getter.ShouldNotBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_PrecompilesComplexFormBindGraph()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupComplexFormEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            // the [FromForm] prop type itself is new'd up on every request
            Config.BndOpts.ReflectionCache.TryGetValue(typeof(ComplexFormDto), out var formDef).ShouldBeTrue();
            formDef!.ObjectFactory.ShouldNotBeNull();

            // nested complex node: factory + the setter ComplexBindMeta compiles (the binder's own ctor only does top level dto props)
            Config.BndOpts.ReflectionCache.TryGetValue(typeof(FormNestedDto), out var nestedDef).ShouldBeTrue();
            nestedDef!.ObjectFactory.ShouldNotBeNull();
            nestedDef.Properties!.TryGetValue(typeof(FormNestedDto).GetProperty(nameof(FormNestedDto.City))!, out var cityDef).ShouldBeTrue();
            cityDef!.Setter.ShouldNotBeNull();

            // complex collection element node
            Config.BndOpts.ReflectionCache.TryGetValue(typeof(FormItemDto), out var itemDef).ShouldBeTrue();
            itemDef!.ObjectFactory.ShouldNotBeNull();
            itemDef.Properties!.TryGetValue(typeof(FormItemDto).GetProperty(nameof(FormItemDto.Qty))!, out var qtyDef).ShouldBeTrue();
            qtyDef!.Setter.ShouldNotBeNull();

            // simple + simple-collection props of the root node
            formDef.Properties!.TryGetValue(typeof(ComplexFormDto).GetProperty(nameof(ComplexFormDto.Name))!, out var nameDef).ShouldBeTrue();
            nameDef!.Setter.ShouldNotBeNull();
            nameDef.ValueParser.ShouldNotBeNull();
            formDef.Properties.TryGetValue(typeof(ComplexFormDto).GetProperty(nameof(ComplexFormDto.Tags))!, out var tagsDef).ShouldBeTrue();
            tagsDef!.Setter.ShouldNotBeNull();
            tagsDef.ListFactory.ShouldNotBeNull();  // the MakeGenericType the commit is about
            tagsDef.ValueParser.ShouldNotBeNull();  // element parser for the simple collection

            // the complex collection's List<T> factory, and no element parser (elements are bound recursively)
            formDef.Properties.TryGetValue(typeof(ComplexFormDto).GetProperty(nameof(ComplexFormDto.Items))!, out var itemsDef).ShouldBeTrue();
            itemsDef!.ListFactory.ShouldNotBeNull();
            itemsDef.ElementIsComplex.ShouldBeTrue();
            itemsDef.ElementType.ShouldBe(typeof(FormItemDto));

            // FieldName is ComplexBindMeta's publication sentinel: non-null on every prop in the graph == fully preloaded
            AssertGraphFullyPublished(typeof(ComplexFormDto), []);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_PrecompilesComplexQueryBindGraph()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupComplexQueryEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(ComplexQueryDto), out var queryDef).ShouldBeTrue();
            queryDef!.ObjectFactory.ShouldNotBeNull();

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(QueryNestedDto), out var nestedDef).ShouldBeTrue();
            nestedDef!.ObjectFactory.ShouldNotBeNull();
            nestedDef.Properties!.TryGetValue(typeof(QueryNestedDto).GetProperty(nameof(QueryNestedDto.Zip))!, out var zipDef).ShouldBeTrue();
            zipDef!.Setter.ShouldNotBeNull();
            zipDef.FieldName.ShouldNotBeNull();

            AssertGraphFullyPublished(typeof(ComplexQueryDto), []);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_ComplexBindGraph_DoesNotInstantiateAbstractOrFormFileNodes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupUnconstructibleFormEp)]);
        var app = builder.Build();

        try
        {
            // must not throw at startup - the binder only instantiates these nodes if data is actually posted for them
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            if (Config.BndOpts.ReflectionCache.TryGetValue(typeof(AbstractFormNestedDto), out var abstractDef))
                abstractDef!.ObjectFactory.ShouldBeNull();

            if (Config.BndOpts.ReflectionCache.TryGetValue(typeof(IFormFile), out var fileDef))
                fileDef!.ObjectFactory.ShouldBeNull();

            // the constructible sibling in the same graph still gets warmed
            Config.BndOpts.ReflectionCache.TryGetValue(typeof(FormNestedDto), out var nestedDef).ShouldBeTrue();
            nestedDef!.ObjectFactory.ShouldNotBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_ComplexBindGraph_HandlesSelfReferencingNode()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupRecursiveFormEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(RecursiveFormDto), out var recursiveDef).ShouldBeTrue();
            recursiveDef!.ObjectFactory.ShouldNotBeNull();
            recursiveDef.Properties!.TryGetValue(typeof(RecursiveFormDto).GetProperty(nameof(RecursiveFormDto.Child))!, out var childDef).ShouldBeTrue();
            childDef!.Setter.ShouldNotBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_ComplexBindGraph_HandlesIndexersAndNestedCollections()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupIndexerFormEp)]);
        var app = builder.Build();

        try
        {
            // neither a node declaring an indexer nor an unsupported List<List<T>> may fail startup - the binder
            // rejects the latter at bind time, and warmup must not escalate that into a boot failure.
            var ex = Record.Exception(() => app.UseFastEndpoints(c => c.Endpoints.Warmup()));
            ex.ShouldBeNull();

            // the rest of the graph is still warmed
            Config.BndOpts.ReflectionCache.TryGetValue(typeof(IndexerFormDto), out var formDef).ShouldBeTrue();
            formDef!.Properties!.TryGetValue(typeof(IndexerFormDto).GetProperty(nameof(IndexerFormDto.Name))!, out var nameDef).ShouldBeTrue();
            nameDef!.FieldName.ShouldNotBeNull();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// walks the graph the same way <c>ComplexSourceBinder</c> does and asserts every property it would touch at request
    /// time already has its metadata published (<c>FieldName</c> is the sentinel <c>ComplexBindMeta</c> sets last).
    /// </summary>
    static void AssertGraphFullyPublished(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;

        Config.BndOpts.ReflectionCache.TryGetValue(type, out var typeDef).ShouldBeTrue($"no reflection cache entry for [{type.Name}]");

        foreach (var prop in type.GetProperties())
        {
            if (prop.GetSetMethod()?.IsPublic is not true)
                continue;

            typeDef!.Properties!.TryGetValue(prop, out var propDef).ShouldBeTrue($"no prop entry for [{type.Name}.{prop.Name}]");
            propDef!.FieldName.ShouldNotBeNull($"complex bind metadata not preloaded for [{type.Name}.{prop.Name}]");
            propDef.Setter.ShouldNotBeNull($"setter not preloaded for [{type.Name}.{prop.Name}]");

            if (propDef.IsFormFile || propDef.IsFormFileCollection)
                continue;

            if (propDef.IsCollection)
            {
                if (propDef is { ElementIsComplex: true, ElementType: { } tElement })
                    AssertGraphFullyPublished(tElement, visited);
                else
                    propDef.ValueParser.ShouldNotBeNull($"element parser not preloaded for [{type.Name}.{prop.Name}]");
            }
            else if (propDef.IsComplex)
                AssertGraphFullyPublished(propDef.UnderlyingType!, visited);
            else
                propDef.ValueParser.ShouldNotBeNull($"parser not preloaded for [{type.Name}.{prop.Name}]");
        }
    }

    static void ResetServiceResolver()
    {
        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }
}

file sealed class CountingBinder<TRequest> : IRequestBinder<TRequest>
    where TRequest : notnull
{
    public CountingBinder()
    {
        CountingBinder.InstanceCount++;
    }

    public ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct)
        => ValueTask.FromResult(default(TRequest)!);
}

file static class CountingBinder
{
    public static int InstanceCount { get; set; }

    public static void Reset()
        => InstanceCount = 0;
}

file sealed class WarmupEpA : EndpointWithoutRequest
{
    public override void Configure()
        => Get("warmup-ep-a");

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupEpB : EndpointWithoutRequest
{
    public override void Configure()
        => Get("warmup-ep-b");

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupNestedRequest
{
    [Required]
    public string? Name { get; set; }

    public NestedDto? Nested { get; set; }

    public List<NestedItemDto>? Items { get; set; }
}

file sealed class NestedDto
{
    [Required]
    public string? City { get; set; }
}

file sealed class NestedItemDto
{
    [Range(1, 10)]
    public int Qty { get; set; }
}

file sealed class WarmupNestedEp : Endpoint<WarmupNestedRequest>
{
    public override void Configure()
        => Post("warmup-nested-ep");

    public override Task HandleAsync(WarmupNestedRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupAbstractNestedRequest
{
    [Required]
    public string? Name { get; set; }

    public AbstractNestedDto? Child { get; set; }
}

file abstract class AbstractNestedDto
{
    [Required]
    public string? City { get; set; }
}

file sealed class WarmupAbstractNestedEp : Endpoint<WarmupAbstractNestedRequest>
{
    public override void Configure()
        => Post("warmup-abstract-nested-ep");

    public override Task HandleAsync(WarmupAbstractNestedRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupNoPublicCtorNestedRequest
{
    [Required]
    public string? Name { get; set; }

    public NoPublicCtorNestedDto? Child { get; set; }
}

file sealed class NoPublicCtorNestedDto
{
    NoPublicCtorNestedDto() { }

    public static NoPublicCtorNestedDto Create(string city)
        => new() { City = city };

    [Required]
    public string? City { get; set; }
}

file sealed class WarmupNoPublicCtorNestedEp : Endpoint<WarmupNoPublicCtorNestedRequest>
{
    public override void Configure()
        => Post("warmup-no-public-ctor-nested-ep");

    public override Task HandleAsync(WarmupNoPublicCtorNestedRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupDerivedCollectionRequest
{
    [Required]
    public string? Name { get; set; }

    public NestedItemCollection? Items { get; set; }
}

file sealed class NestedItemCollection : List<DerivedCollectionItemDto>;

file sealed class DerivedCollectionItemDto
{
    [Required]
    public string? Label { get; set; }
}

file sealed class WarmupDerivedCollectionEp : Endpoint<WarmupDerivedCollectionRequest>
{
    public override void Configure()
        => Post("warmup-derived-collection-ep");

    public override Task HandleAsync(WarmupDerivedCollectionRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupComplexFormRequest
{
    [FromForm]
    public ComplexFormDto? Form { get; set; }
}

file sealed class ComplexFormDto
{
    public string? Name { get; set; }

    public List<string>? Tags { get; set; }

    public FormNestedDto? Nested { get; set; }

    public List<FormItemDto>? Items { get; set; }

    public IFormFile? File { get; set; }

    public IEnumerable<IFormFile>? Files { get; set; }
}

file sealed class FormNestedDto
{
    public string? City { get; set; }
}

file sealed class FormItemDto
{
    public int Qty { get; set; }
}

file sealed class WarmupComplexFormEp : Endpoint<WarmupComplexFormRequest>
{
    public override void Configure()
    {
        Post("warmup-complex-form-ep");
        AllowFormData();
    }

    public override Task HandleAsync(WarmupComplexFormRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupComplexQueryRequest
{
    [FromQuery]
    public ComplexQueryDto? Query { get; set; }
}

file sealed class ComplexQueryDto
{
    public string? Term { get; set; }

    public QueryNestedDto? Nested { get; set; }
}

file sealed class QueryNestedDto
{
    public string? Zip { get; set; }
}

file sealed class WarmupComplexQueryEp : Endpoint<WarmupComplexQueryRequest>
{
    public override void Configure()
        => Get("warmup-complex-query-ep");

    public override Task HandleAsync(WarmupComplexQueryRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupUnconstructibleFormRequest
{
    [FromForm]
    public UnconstructibleFormDto? Form { get; set; }
}

file sealed class UnconstructibleFormDto
{
    public AbstractFormNestedDto? Abstract { get; set; }

    public FormNestedDto? Nested { get; set; }
}

file abstract class AbstractFormNestedDto
{
    public string? City { get; set; }
}

file sealed class WarmupUnconstructibleFormEp : Endpoint<WarmupUnconstructibleFormRequest>
{
    public override void Configure()
    {
        Post("warmup-unconstructible-form-ep");
        AllowFormData();
    }

    public override Task HandleAsync(WarmupUnconstructibleFormRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupRecursiveFormRequest
{
    [FromForm]
    public RecursiveFormDto? Form { get; set; }
}

file sealed class RecursiveFormDto
{
    public string? Name { get; set; }

    public RecursiveFormDto? Child { get; set; }
}

file sealed class WarmupIndexerFormRequest
{
    [FromForm]
    public IndexerFormDto? Form { get; set; }
}

file sealed class IndexerFormDto
{
    public string? Name { get; set; }

    public List<List<FormItemDto>>? Matrix { get; set; } // element type List<T> exposes a public Item[int] indexer

    public SelfIndexingDto? Indexed { get; set; }
}

file sealed class SelfIndexingDto
{
    public string? Label { get; set; }

    public string this[int i]
    {
        get => Label!;
        set => Label = value;
    }
}

file sealed class WarmupIndexerFormEp : Endpoint<WarmupIndexerFormRequest>
{
    public override void Configure()
    {
        Post("warmup-indexer-form-ep");
        AllowFormData();
    }

    public override Task HandleAsync(WarmupIndexerFormRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupRecursiveFormEp : Endpoint<WarmupRecursiveFormRequest>
{
    public override void Configure()
    {
        Post("warmup-recursive-form-ep");
        AllowFormData();
    }

    public override Task HandleAsync(WarmupRecursiveFormRequest req, CancellationToken ct)
        => Task.CompletedTask;
}

