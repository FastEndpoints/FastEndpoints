using FastEndpoints.Testing;
using Xunit;
using static Ordering.PriorityTestHelpers;

namespace Ordering;

public class TestCollectionOrdererTests
{
    readonly TestCollectionOrderer _sut = new();

    [Fact]
    public void orders_collections_by_priority_ascending()
    {
        var c1 = FakeTestCollection(typeof(CollectionDefPriority1));
        var c2 = FakeTestCollection(typeof(CollectionDefPriority2));
        var c3 = FakeTestCollection(typeof(CollectionDefPriority3));

        // supply in reverse order
        var collections = new[] { c3, c2, c1 };

        var result = _sut.OrderTestCollections(collections).ToList();

        result.Count.ShouldBe(3);
        result[0].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority1));
        result[1].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority2));
        result[2].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority3));
    }

    [Fact]
    public void unordered_collections_appear_after_ordered_ones()
    {
        var c1 = FakeTestCollection(typeof(CollectionDefPriority1));
        var cNone = FakeTestCollection(typeof(CollectionDefNoPriority));
        var c2 = FakeTestCollection(typeof(CollectionDefPriority2));

        var collections = new[] { cNone, c2, c1 };

        var result = _sut.OrderTestCollections(collections).ToList();

        result.Count.ShouldBe(3);
        result[0].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority1));
        result[1].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority2));
        result[2].CollectionDefinition.ShouldBe(typeof(CollectionDefNoPriority));
    }

    [Fact]
    public void collection_with_null_definition_is_unordered()
    {
        var c1 = FakeTestCollection(typeof(CollectionDefPriority1));
        var cNull = FakeTestCollection(null);

        var collections = new[] { cNull, c1 };

        var result = _sut.OrderTestCollections(collections).ToList();

        result.Count.ShouldBe(2);
        result[0].CollectionDefinition.ShouldBe(typeof(CollectionDefPriority1));
        result[1].CollectionDefinition.ShouldBeNull();
    }

    [Fact]
    public void empty_input_returns_empty()
    {
        var result = _sut.OrderTestCollections(Array.Empty<Xunit.v3.IXunitTestCollection>());

        result.ShouldBeEmpty();
    }

    [Fact]
    public void duplicate_priorities_are_stable()
    {
        var cA = FakeTestCollection(typeof(CollectionDefPriority1));
        var cB = FakeTestCollection(typeof(CollectionDefPriority1));

        var collections = new[] { cA, cB };

        var result = _sut.OrderTestCollections(collections).ToList();

        result.Count.ShouldBe(2);
        result[0].ShouldBeSameAs(cA);
        result[1].ShouldBeSameAs(cB);
    }
}