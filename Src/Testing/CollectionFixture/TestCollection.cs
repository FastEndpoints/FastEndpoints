using Xunit;

namespace FastEndpoints.Testing;

/// <summary>
/// abstract class for creating a collection definition
/// </summary>
/// <typeparam name="TAppFixture">the type of the app fixture that will last for the full lifetime of the test-collection</typeparam>
public abstract class TestCollection<TAppFixture> : ICollectionFixture<TAppFixture>
    where TAppFixture : BaseFixture { }

/// <summary>
/// abstract class for creating a collection definition
/// </summary>
/// <typeparam name="TAppFixture">the type of the app fixture that will last for the full lifetime of the test-collection</typeparam>
/// <typeparam name="TState">the type of the shared state fixture that will last for the full lifetime of the test-collection</typeparam>
public abstract class TestCollection<TAppFixture, TState> : TestCollection<TAppFixture>, ICollectionFixture<TState>
    where TAppFixture : BaseFixture
    where TState : StateFixture;