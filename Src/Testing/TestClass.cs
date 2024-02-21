using Bogus;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace FastEndpoints.Testing;

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
///     to share common state between multiple test-methods of the same test-class, you can inherit the <see cref="TestClass{TAppFixture,TState}" /> abstract class and
///     provide an additional "state fixture" for the test-class.
///     </para>
/// </typeparam>
[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
public abstract class TestClass<TAppFixture>(TAppFixture a, ITestOutputHelper o) : IAsyncLifetime, IClassFixture<TAppFixture> where TAppFixture : BaseFixture
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
    [Obsolete("Use the 'App' property going forward.", false)]
    protected TAppFixture Fixture => App;

    /// <summary>
    /// app fixture that is shared among all tests of this class
    /// </summary>
    /// <remarks>
    /// NOTE: this property will be deprecated in the future. use the <see cref="App" /> property instead.
    /// </remarks>
    [Obsolete("Use the 'App' property going forward.", false)]
    protected TAppFixture Fx => App;

    /// <summary>
    /// xUnit test output helper
    /// </summary>
    protected ITestOutputHelper Output { get; } = o;

    /// <summary>
    /// bogus data generator
    /// </summary>
    protected Faker Fake => App.Fake;

    //TODO: remove Fixture and Fx properties at v6.0

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
/// the type of the app fixture. an app fixture is an implementation of <see cref="AppFixture{TProgram}" /> abstract class which is a uniquely configured running
/// instance of your application being tested (sut). the app fixture instance is created only once before any of the test methods are executed and torn down after all
/// test methods of the class have run. all test methods of the test-class will be accessing that same fixture instance per test run. the underlying WAF instance
/// however is cached and reused per each derived app fixture type in order to speed up test execution. i.e. it's recommended to use the same derived app fixture type
/// with multiple test-classes.
/// </typeparam>
/// <typeparam name="TState">the type of the shared state fixture. implement a "state fixture" by inheriting <see cref="StateFixture" /> abstract class.</typeparam>
public abstract class TestClass<TAppFixture, TState>(TAppFixture a, TState s, ITestOutputHelper o) : TestClass<TAppFixture>(a, o), IClassFixture<TState>
    where TAppFixture : BaseFixture where TState : StateFixture
{
    /// <summary>
    /// the shared state fixture for the purpose of sharing some data across all test-methods of this test-class.
    /// </summary>
    protected TState State { get; } = s;
}