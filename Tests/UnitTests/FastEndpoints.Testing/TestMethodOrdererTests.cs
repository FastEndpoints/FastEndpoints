using FastEndpoints.Testing;
using Xunit;
using static Ordering.PriorityTestHelpers;

namespace Ordering;

public class TestMethodOrdererTests
{
    readonly TestMethodOrderer _sut = new();

    [Fact]
    public void orders_test_methods_by_priority_ascending()
    {
        var m1 = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!);
        var m2 = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority2))!);
        var m3 = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority3))!);

        var result = _sut.OrderTestMethods([m3, m2, m1]).ToList();

        result.Count.ShouldBe(3);
        result[0].ShouldBeSameAs(m1);
        result[1].ShouldBeSameAs(m2);
        result[2].ShouldBeSameAs(m3);
    }

    [Fact]
    public void unordered_methods_appear_after_ordered_ones()
    {
        var m1 = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!);
        var mNone = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_NoPriority))!);
        var m3 = FakeTestMethod(typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority3))!);

        var result = _sut.OrderTestMethods([mNone, m3, m1]).ToList();

        result.Count.ShouldBe(3);
        result[0].ShouldBeSameAs(m1);
        result[1].ShouldBeSameAs(m3);
        result[2].ShouldBeSameAs(mNone);
    }

    [Fact]
    public void empty_input_returns_empty()
        => _sut.OrderTestMethods(Array.Empty<Xunit.v3.IXunitTestMethod>()).ShouldBeEmpty();
}
