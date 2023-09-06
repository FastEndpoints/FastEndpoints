using Bogus;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace FastEndpoints.Testing;

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute"/>
/// </summary>
/// <typeparam name="TFixture">
/// the type of the test fixture. a fixture is a shared data context for all tests of this class.
/// the fixture is instantiated before any of the tests are executed and torn down after all tests have run.
/// fixtures are implemented by inheriting <see cref="TestFixture{TProgram}"/> abstract class.
/// </typeparam>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public abstract class TestClass<TFixture> : IClassFixture<TFixture> where TFixture : class, IFixture
{
    /// <summary>
    /// fixture data that is shared among all tests of this class
    /// </summary>
    protected TFixture Fixture { get; init; }

#pragma warning disable IDE1006
    /// <summary>
    /// fixture data that is shared among all tests of this class
    /// </summary>
    protected TFixture fx => Fixture;
#pragma warning restore IDE1006

    /// <summary>
    /// xUnit test output helper
    /// </summary>
    protected ITestOutputHelper Output { get; init; }

    /// <summary>
    /// bogus data generator
    /// </summary>
    protected Faker Fake => Fixture.Fake;

    protected TestClass(TFixture f, ITestOutputHelper o)
    {
        Fixture = f;
        Output = o;
    }
}