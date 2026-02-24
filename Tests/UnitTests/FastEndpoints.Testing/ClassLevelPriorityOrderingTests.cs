using FastEndpoints.Testing;
using Xunit;
using static Ordering.PriorityTestHelpers;

namespace Ordering;

public class ClassLevelPriorityOrderingTests
{
    [Fact]
    public void orders_test_cases_by_class_priority_ascending()
    {
        var caseP3 = FakeTestCaseForClass(typeof(ClassWithPriority3));
        var caseP1 = FakeTestCaseForClass(typeof(ClassWithPriority1));
        var caseP2 = FakeTestCaseForClass(typeof(ClassWithPriority2));

        // supply in scrambled order: 3, 1, 2
        var cases = new[] { caseP3, caseP1, caseP2 };

        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(cases).ToList();

        result.Count.ShouldBe(3);
        result[0].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority1));
        result[1].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority2));
        result[2].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority3));
    }

    [Fact]
    public void unordered_classes_appear_after_ordered_ones()
    {
        var caseP1 = FakeTestCaseForClass(typeof(ClassWithPriority1));
        var caseNone = FakeTestCaseForClass(typeof(ClassWithNoPriority));
        var caseP2 = FakeTestCaseForClass(typeof(ClassWithPriority2));

        var cases = new[] { caseNone, caseP2, caseP1 };

        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(cases).ToList();

        result.Count.ShouldBe(3);
        result[0].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority1));
        result[1].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority2));
        result[2].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithNoPriority));
    }

    [Fact]
    public void multiple_cases_from_same_class_stay_together()
    {
        var caseP2A = FakeTestCaseForClass(typeof(ClassWithPriority2), "class2-case-a");
        var caseP2B = FakeTestCaseForClass(typeof(ClassWithPriority2), "class2-case-b");
        var caseP1 = FakeTestCaseForClass(typeof(ClassWithPriority1));

        // class2 cases before class1
        var cases = new[] { caseP2A, caseP2B, caseP1 };

        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(cases).ToList();

        result.Count.ShouldBe(3);

        // class1 (priority 1) should come first
        result[0].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority1));

        // both class2 (priority 2) cases after
        result[1].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority2));
        result[2].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority2));

        // and their original relative order is preserved
        result[1].ShouldBeSameAs(caseP2A);
        result[2].ShouldBeSameAs(caseP2B);
    }

    [Fact]
    public void empty_input_returns_empty()
    {
        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(Array.Empty<Xunit.v3.IXunitTestCase>());

        result.ShouldBeEmpty();
    }

    [Fact]
    public void all_unordered_cases_preserves_input_order()
    {
        var caseA = FakeTestCaseForClass(typeof(ClassWithNoPriority), "a");
        var caseB = FakeTestCaseForClass(typeof(ClassWithNoPriority), "b");

        var cases = new[] { caseA, caseB };

        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(cases).ToList();

        result.Count.ShouldBe(2);
        result[0].ShouldBeSameAs(caseA);
        result[1].ShouldBeSameAs(caseB);
    }

    [Fact]
    public void four_classes_in_reverse_order_sorted_correctly()
    {
        var caseP4 = FakeTestCaseForClass(typeof(ClassWithPriority3)); // reusing priority 3 for 4th slot
        var caseP3 = FakeTestCaseForClass(typeof(ClassWithPriority3));
        var caseP2 = FakeTestCaseForClass(typeof(ClassWithPriority2));
        var caseP1 = FakeTestCaseForClass(typeof(ClassWithPriority1));

        var cases = new[] { caseP4, caseP3, caseP2, caseP1 };

        var result = TestAssemblyRunner.OrderAccordingToClassLevelPriority(cases).ToList();

        result.Count.ShouldBe(4);
        result[0].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority1));
        result[1].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority2));
        result[2].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority3));
        result[3].TestMethod.TestClass.Class.ShouldBe(typeof(ClassWithPriority3));
    }
}