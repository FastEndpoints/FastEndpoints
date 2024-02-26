using Xunit.Priority;

namespace FixtureTests;

public sealed class MyStateFixture : StateFixture
{
    public int Id { get; set; }

    protected override async Task SetupAsync()
    {
        Id = 123;
        await Task.CompletedTask;
    }

    protected override async Task TearDownAsync()
    {
        Id = 0;
        await Task.CompletedTask;
    }
}

public class StateFixtureTests(MyStateFixture State) : TestBase<AppFixture, MyStateFixture>
{
    [Fact, Priority(1)]
    public async Task State_Is_Injected_By_Xunit()
        => State.Id.Should().Be(123);

    [Fact, Priority(2)]
    public async Task State_Modification()
    {
        State.Id.Should().Be(123);
        State.Id = 321;
    }

    [Fact, Priority(3)]
    public async Task Verify_State_Modification()
        => State.Id.Should().Be(321);
}