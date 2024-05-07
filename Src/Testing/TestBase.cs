using Bogus;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace FastEndpoints.Testing;

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public abstract class TestBase : IAsyncLifetime, IFaker
{
    static readonly Faker _faker = new();

    public Faker Fake => _faker;

    // ReSharper disable VirtualMemberNeverOverridden.Global

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the test-class.
    /// it is run before any of the test-methods of the class is executed.
    /// </summary>
    protected virtual Task SetupAsync()
        => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the test-class.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual Task TearDownAsync()
        => Task.CompletedTask;

    Task IAsyncLifetime.InitializeAsync()
        => SetupAsync();

    Task IAsyncLifetime.DisposeAsync()
        => TearDownAsync();
}

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
/// <typeparam name="TAppFixture">
///     <para>
///     the type of the app fixture. an app fixture is an implementation of <see cref="AppFixture{TProgram}" /> abstract class which is a uniquely configured running
///     instance of your application being tested (sut). the app fixture instance is created only once before any of the test methods are executed and torn down after all
///     test methods of the class have run. all test methods of the test-class will be accessing that same fixture instance per test run. the underlying WAF instance
///     however is cached and reused per each derived app fixture type in order to speed up test execution. i.e. it's recommended to use the same derived app fixture type
///     with multiple test-classes.
///     </para>
///     <para>
///     to share common state between multiple test-methods of the same test-class, you can inherit the <see cref="TestBase{TAppFixture,TState}" /> abstract class and
///     provide an additional "state fixture" for the test-class.
///     </para>
/// </typeparam>
public abstract class TestBase<TAppFixture> : TestBase, IClassFixture<TAppFixture> where TAppFixture : BaseFixture;

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
/// <typeparam name="TAppFixture">
/// the type of the app fixture. an app fixture is an implementation of <see cref="AppFixture{TProgram}" /> abstract class which is a uniquely configured running
/// instance of your application being tested (sut). the app fixture instance is created only once before any of the test methods are executed and torn down after all
/// test methods of the class have run. all test methods of the test-class will be accessing that same fixture instance per test run. the underlying WAF instance
/// however is cached and reused per each derived app fixture type in order to speed up test execution. i.e. it's recommended to use the same derived app fixture type
/// with multiple test-classes.
/// </typeparam>
/// <typeparam name="TState">the type of the shared state fixture. implement a "state fixture" by inheriting <see cref="StateFixture" /> abstract class.</typeparam>
public abstract class TestBase<TAppFixture, TState> : TestBase<TAppFixture>, IClassFixture<TState> where TAppFixture : BaseFixture where TState : StateFixture;

[Obsolete("Use the TestBase<TAppFixture> class going forward. This class will be removed at the next major version jump.")]
public abstract class TestClass<TAppFixture>(TAppFixture a, ITestOutputHelper o) : TestBase<TAppFixture> where TAppFixture : BaseFixture
{
    /// <summary>
    /// app fixture that is shared among all tests of this class
    /// </summary>
    protected TAppFixture App { get; } = a;

    /// <summary>
    /// app fixture that is shared among all tests of this class
    /// </summary>
    /// <remarks>
    /// NOTE: this property will be deprecated in the future. use the <see cref="App" /> property instead.
    /// </remarks>
    protected TAppFixture Fixture => App;

    /// <summary>
    /// app fixture that is shared among all tests of this class
    /// </summary>
    /// <remarks>
    /// NOTE: this property will be deprecated in the future. use the <see cref="App" /> property instead.
    /// </remarks>
    protected TAppFixture Fx => App;

    /// <summary>
    /// xUnit test output helper
    /// </summary>
    protected ITestOutputHelper Output { get; } = o;

    //TODO: remove this class at v6.0. only here for backwards compatibility.
}