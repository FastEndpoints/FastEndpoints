using FastEndpoints.Testing;
using Xunit;
using static Ordering.PriorityTestHelpers;

namespace Ordering;

public class TestCaseOrdererTests
{
    readonly TestCaseOrderer _sut = new();

    [Fact]
    public void orders_test_cases_by_method_priority_ascending()
    {
        var m1 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!;
        var m2 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority2))!;
        var m3 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority3))!;

        // supply in reverse order (3, 2, 1)
        var cases = new[]
        {
            FakeTestCaseForMethod(m3),
            FakeTestCaseForMethod(m2),
            FakeTestCaseForMethod(m1)
        };

        var result = _sut.OrderTestCases(cases);

        result.Count.ShouldBe(3);
        var ordered = result.ToList();

        ordered[0].TestMethod.Method.ShouldBe(m1);
        ordered[1].TestMethod.Method.ShouldBe(m2);
        ordered[2].TestMethod.Method.ShouldBe(m3);
    }

    [Fact]
    public void unordered_cases_appear_after_ordered_ones()
    {
        var m1 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!;
        var mNone = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_NoPriority))!;
        var m3 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority3))!;

        var cases = new[]
        {
            FakeTestCaseForMethod(mNone),
            FakeTestCaseForMethod(m3),
            FakeTestCaseForMethod(m1)
        };

        var result = _sut.OrderTestCases(cases).ToList();

        result.Count.ShouldBe(3);

        // priority 1 first, then priority 3, then the unordered one
        result[0].TestMethod.Method.ShouldBe(m1);
        result[1].TestMethod.Method.ShouldBe(m3);
        result[2].TestMethod.Method.ShouldBe(mNone);
    }

    [Fact]
    public void all_unordered_cases_preserves_input_order()
    {
        var mNone = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_NoPriority))!;
        var m1 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!;

        // two cases without priority. use distinct method infos that have no attribute
        var caseA = FakeTestCaseForMethod(mNone);
        var caseB = FakeTestCaseForMethod(mNone);

        var cases = new[] { caseA, caseB };

        var result = _sut.OrderTestCases(cases).ToList();

        result.Count.ShouldBe(2);
        result[0].ShouldBeSameAs(caseA);
        result[1].ShouldBeSameAs(caseB);
    }

    [Fact]
    public void empty_input_returns_empty()
    {
        var result = _sut.OrderTestCases(Array.Empty<Xunit.v3.IXunitTestCase>());

        result.ShouldBeEmpty();
    }

    [Fact]
    public void duplicate_priorities_are_stable()
    {
        // two cases with the same priority should both appear. order among equals is stable
        var m1 = typeof(MethodPriorityStubs).GetMethod(nameof(MethodPriorityStubs.Method_Priority1))!;

        var caseA = FakeTestCaseForMethod(m1);
        var caseB = FakeTestCaseForMethod(m1);

        var cases = new[] { caseA, caseB };

        var result = _sut.OrderTestCases(cases).ToList();

        result.Count.ShouldBe(2);
        result[0].ShouldBeSameAs(caseA);
        result[1].ShouldBeSameAs(caseB);
    }
}