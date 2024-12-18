namespace FixtureTests;

public class AssemblyFixtureTests
{
    public class GlobalApp : AppFixture<Web.Program>
    {
        public static Lazy<Counter> Count { get; } = new(() => new(0));

        protected override ValueTask SetupAsync()
        {
            Count.Value.Number += 1;

            return ValueTask.CompletedTask;
        }

        protected override ValueTask TearDownAsync()
        {
            Count.Value.Number += 1;

            return ValueTask.CompletedTask;
        }

        public class Counter(int val)
        {
            public int Number { get; set; } = val;
        }
    }

#pragma warning disable xUnit1041

    public class ClassA(GlobalApp App) : TestBaseWithAssemblyFixture<GlobalApp>
    {
        //[Fact]
        public void Fixture_SetupAsync_Called_Once()
        {
            App.Should().NotBeNull();
            GlobalApp.Count.Value.Number.Should().Be(1);
        }
    }

    public class ClassB(GlobalApp App) : TestBaseWithAssemblyFixture<GlobalApp>
    {
        //[Fact]
        public void Fixture_SetupAsync_Called_Once()
        {
            App.Should().NotBeNull();
            GlobalApp.Count.Value.Number.Should().Be(1);
        }
    }
}