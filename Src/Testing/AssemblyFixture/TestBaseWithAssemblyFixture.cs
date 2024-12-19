using Bogus;
using Xunit;

namespace FastEndpoints.Testing;

/// <summary>
/// abstract class for implementing a test-class with an assembly level app fixture.
/// </summary>
/// <typeparam name="TAppFixture">
/// the type of the assembly level app fixture.
/// </typeparam>
public abstract class TestBaseWithAssemblyFixture<TAppFixture> : IAsyncLifetime, IFaker, IAssemblyFixture<TAppFixture> where TAppFixture : BaseFixture
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
    /// override this method if you'd like to do some one-time setup for the test-class.
    /// it is run before any of the test-methods of the class is executed.
    /// </summary>
    protected virtual ValueTask SetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the test-class.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual ValueTask TearDownAsync()
        => ValueTask.CompletedTask;

    ValueTask IAsyncLifetime.InitializeAsync()
        => SetupAsync();

    ValueTask IAsyncDisposable.DisposeAsync()
        => TearDownAsync();
}

/// <summary>
/// abstract class for implementing a test-class with an assembly level app fixture.
/// </summary>
/// <typeparam name="TAppFixture">
/// the type of the assembly level/ global app fixture.
/// </typeparam>
/// <typeparam name="TState">the type of the shared state fixture that will only last during the execution of this test-class</typeparam>
public abstract class TestBaseWithAssemblyFixture<TAppFixture, TState> : TestBaseWithAssemblyFixture<TAppFixture>, IClassFixture<TState>
    where TAppFixture : BaseFixture where TState : StateFixture;