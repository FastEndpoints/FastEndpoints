using Bogus;
using Xunit;

namespace FastEndpoints.Testing;

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
[TestCaseOrderer(typeof(TestCaseOrderer))]
public abstract class TestBase : IAsyncLifetime, IFaker
{
    static readonly Faker _faker = new();

    public Faker Fake => _faker;

#pragma warning disable CA1822
    public ITestContext Context => TestContext.Current;
    public CancellationToken Cancellation => TestContext.Current.CancellationToken;
    public ITestOutputHelper Output
        => TestContext.Current.TestOutputHelper ?? throw new InvalidOperationException("Test output helper is not available in the current context!");
#pragma warning restore CA1822

    // ReSharper disable VirtualMemberNeverOverridden.Global

    /// <summary>
    /// override this method if you'd like to do some setup before each test-case gets executed.
    /// it is run per test and is analogous to an async constructor for the test-class.
    /// TIP: xunit creates a fresh instance of the test-class per test.
    /// </summary>
    protected virtual ValueTask SetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some teardown/cleanup after each test-case has completed.
    /// it is run per test and is analogous to an async destructor for the test-class.
    /// TIP: xunit creates a fresh instance of the test-class per test.
    /// </summary>
    protected virtual ValueTask TearDownAsync()
        => ValueTask.CompletedTask;

    ValueTask IAsyncLifetime.InitializeAsync()
        => SetupAsync();

    ValueTask IAsyncDisposable.DisposeAsync()
        => TearDownAsync();
}

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
/// <typeparam name="TAppFixture">
///     <para>
///     the type of the app fixture. an app fixture is an implementation of <see cref="AppFixture{TProgram}" /> abstract class which is a uniquely configured
///     running
///     instance of your application being tested (sut). the app fixture instance is created only once before any of the test methods are executed and torn
///     down after all
///     test methods of the class have run. all test methods of the test-class will be accessing that same fixture instance per test run. the underlying WAF
///     instance
///     however is cached and reused per each derived app fixture type in order to speed up test execution. i.e. it's recommended to use the same derived app
///     fixture type
///     with multiple test-classes.
///     </para>
///     <para>
///     to share common state between multiple test-methods of the same test-class, you can inherit the <see cref="TestBase{TAppFixture,TState}" /> abstract
///     class and
///     provide an additional "state fixture" for the test-class.
///     </para>
/// </typeparam>
public abstract class TestBase<TAppFixture> : TestBase, IClassFixture<TAppFixture> where TAppFixture : BaseFixture;

/// <summary>
/// abstract class for implementing a test-class, which is a collection of integration tests that may be related to each other.
/// test methods can be run in a given order by decorating the methods with <see cref="PriorityAttribute" />
/// </summary>
/// <typeparam name="TAppFixture">
/// the type of the app fixture. an app fixture is an implementation of <see cref="AppFixture{TProgram}" /> abstract class which is a uniquely configured
/// running
/// instance of your application being tested (sut). the app fixture instance is created only once before any of the test methods are executed and torn down
/// after all
/// test methods of the class have run. all test methods of the test-class will be accessing that same fixture instance per test run. the underlying WAF
/// instance
/// however is cached and reused per each derived app fixture type in order to speed up test execution. i.e. it's recommended to use the same derived app
/// fixture type
/// with multiple test-classes.
/// </typeparam>
/// <typeparam name="TState">the type of the shared state fixture. implement a "state fixture" by inheriting <see cref="StateFixture" /> abstract class.</typeparam>
public abstract class TestBase<TAppFixture, TState> : TestBase<TAppFixture>, IClassFixture<TState>
    where TAppFixture : BaseFixture where TState : StateFixture;