using FastEndpoints;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace ComplexSourceBinding;

public class ComplexSourceBinderTests
{
    static TDto? BindForm<TDto>(Dictionary<string, StringValues> form, List<ValidationFailure>? failures = null) where TDto : class
    {
        var holder = new Holder<TDto>();
        ComplexSourceBinder.Bind(
            new()
            {
                PropType = typeof(TDto),
                PropSetter = (p, v) => ((Holder<TDto>)p).Value = (TDto?)v
            },
            holder,
            new FormCollection(form),
            failures ?? []);

        return holder.Value;
    }

    static TDto? BindQuery<TDto>(Dictionary<string, StringValues> query) where TDto : class
    {
        var holder = new Holder<TDto>();
        ComplexSourceBinder.Bind(
            new()
            {
                PropType = typeof(TDto),
                PropSetter = (p, v) => ((Holder<TDto>)p).Value = (TDto?)v
            },
            holder,
            new QueryCollection(query),
            []);

        return holder.Value;
    }

    [Fact]
    public void IndexersAreNotBindableProps()
    {
        // an indexer has no bindable field name and no compilable getter/setter
        typeof(IndexedDto).BindableProps().Select(p => p.Name).ShouldBe([nameof(IndexedDto.Label)]);
        typeof(List<int>).BindableProps().ShouldNotContain(p => p.Name == "Item");
    }

    [Fact]
    public void BindsComplexNodeDeclaringAnIndexer()
    {
        var dto = BindForm<IndexedDto>(new() { ["Label"] = "hello" });

        dto!.Label.ShouldBe("hello");
    }

    [Fact]
    public void BindsNestedNodeDeclaringAnIndexer()
    {
        var dto = BindForm<OuterDto>(new() { ["Inner.Label"] = "x" });

        dto!.Inner!.Label.ShouldBe("x");
    }

    [Fact]
    public void IndexerOnRequestDtoDoesNotBreakTheRequestBinder()
    {
        // RequestBinder<T>'s static ctor compiles a setter for every bindable prop
        Should.NotThrow(() => new RequestBinder<IndexedDto>());
    }

    [Fact]
    public void NestedCollectionThrowsDescriptively()
    {
        var ex = Should.Throw<NotSupportedException>(() => BindForm<MatrixDto>(new() { ["Matrix[0][0].Qty"] = "1" }));

        ex.Message.ShouldContain("are themselves collections");
        ex.Message.ShouldContain(nameof(MatrixDto.Matrix));
    }

    [Fact]
    public void NestedCollectionThrowsForQuerySourceToo()
    {
        var ex = Should.Throw<NotSupportedException>(() => BindQuery<MatrixDto>(new() { ["Matrix[0][0].Qty"] = "1" }));

        ex.Message.ShouldContain("query");
    }

    [Fact]
    public void StillBindsAnOrdinaryGraph()
    {
        var dto = BindForm<NormalDto>(
            new()
            {
                ["Name"] = "n",
                ["Tags[0]"] = "a",
                ["Tags[1]"] = "b",
                ["Nested.City"] = "c",
                ["Items[0].Qty"] = "7",
                ["Items[1].Qty"] = "8"
            });

        dto!.Name.ShouldBe("n");
        dto.Tags.ShouldBe(["a", "b"]);
        dto.Nested!.City.ShouldBe("c");
        dto.Items!.Select(i => i.Qty).ShouldBe([7, 8]);
    }
}

file sealed class Holder<T>
{
    public T? Value { get; set; }
}

public sealed class IndexedDto
{
    public string? Label { get; set; }

    public string this[int i]
    {
        get => Label!;
        set => Label = value;
    }
}

public sealed class OuterDto
{
    public IndexedDto? Inner { get; set; }
}

public sealed class MatrixItem
{
    public int Qty { get; set; }
}

public sealed class MatrixDto
{
    public List<List<MatrixItem>>? Matrix { get; set; }
}

public sealed class NormalDto
{
    public string? Name { get; set; }

    public List<string>? Tags { get; set; }

    public NormalNested? Nested { get; set; }

    public List<MatrixItem>? Items { get; set; }
}

public sealed class NormalNested
{
    public string? City { get; set; }
}
