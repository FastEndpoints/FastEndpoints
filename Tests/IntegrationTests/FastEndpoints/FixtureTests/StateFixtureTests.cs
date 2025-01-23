namespace FixtureTests;

public sealed class MyStateFixture : StateFixture
{
    public int Id { get; set; }

    protected override async ValueTask SetupAsync()
    {
        Id = 123;
        await Task.CompletedTask;
    }

    protected override async ValueTask TearDownAsync()
    {
        Id = 0;
        await Task.CompletedTask;
    }
}

public class StateFixtureTests(Sut App, MyStateFixture State) : TestBase<Sut, MyStateFixture>
{
    [Fact]
    public async Task Fixture_Is_Not_Null()
        => App.ShouldNotBeNull();

    [Fact, Priority(1)]
    public async Task State_Is_Injected_By_Xunit()
        => State.Id.ShouldBe(123);

    [Fact, Priority(2)]
    public async Task State_Modification()
    {
        State.Id.ShouldBe(123);
        State.Id = 321;
    }

    [Fact, Priority(3)]
    public async Task Verify_State_Modification()
        => State.Id.ShouldBe(321);
}