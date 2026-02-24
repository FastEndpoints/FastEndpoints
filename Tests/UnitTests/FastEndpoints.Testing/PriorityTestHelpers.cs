using System.Reflection;
using FakeItEasy;
using FastEndpoints.Testing;
using Xunit.v3;

#pragma warning disable CA1822

namespace Ordering;

static class PriorityTestHelpers
{
    [Priority(1)]
    public class ClassWithPriority1
    {
        public void DummyMethod() { }
    }

    [Priority(2)]
    public class ClassWithPriority2
    {
        public void DummyMethod() { }
    }

    [Priority(3)]
    public class ClassWithPriority3
    {
        public void DummyMethod() { }
    }

    public class ClassWithNoPriority
    {
        public void DummyMethod() { }
    }

    public class MethodPriorityStubs
    {
        [Priority(1)]
        public void Method_Priority1() { }

        [Priority(2)]
        public void Method_Priority2() { }

        [Priority(3)]
        public void Method_Priority3() { }

        public void Method_NoPriority() { }
    }

    [Priority(1)]
    public class CollectionDefPriority1;

    [Priority(2)]
    public class CollectionDefPriority2;

    [Priority(3)]
    public class CollectionDefPriority3;

    public class CollectionDefNoPriority;

    public static IXunitTestCase FakeTestCaseForMethod(MethodInfo method)
    {
        var testMethod = A.Fake<IXunitTestMethod>();
        A.CallTo(() => testMethod.Method).Returns(method);

        var testCase = A.Fake<IXunitTestCase>();
        A.CallTo(() => testCase.TestMethod).Returns(testMethod);

        return testCase;
    }

    public static IXunitTestCase FakeTestCaseForClass(Type classType, string? uniqueId = null)
    {
        var testClass = A.Fake<IXunitTestClass>();
        A.CallTo(() => testClass.Class).Returns(classType);
        A.CallTo(() => testClass.UniqueID).Returns(uniqueId ?? classType.FullName!);
        A.CallTo(() => testClass.TestClassName).Returns(classType.FullName!);

        var testMethod = A.Fake<IXunitTestMethod>();
        A.CallTo(() => testMethod.TestClass).Returns(testClass);

        var testCase = A.Fake<IXunitTestCase>();
        A.CallTo(() => testCase.TestMethod).Returns(testMethod);
        A.CallTo(() => testCase.TestClass).Returns(testClass);

        return testCase;
    }

    public static IXunitTestCollection FakeTestCollection(Type? definitionType)
    {
        var collection = A.Fake<IXunitTestCollection>();
        A.CallTo(() => collection.CollectionDefinition).Returns(definitionType);

        return collection;
    }
}