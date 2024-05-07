namespace FixtureTests;

public class CollectionFixtureTests
{
    // collection fixture

    public class CollectionAppFixture : AppFixture<Web.Program>
    {
        public static string Default { get; } = Guid.NewGuid().ToString("N");
        public string Id { get; private set; }
        public int Count { get; private set; }

        protected override async Task SetupAsync()
        {
            //proves setup was run
            Id = Default.Reverse().ToString()!;
            Count++;
        }

        protected override async Task TearDownAsync()
        {
            //proves teardown won't run before collection is complete
            Id = Guid.NewGuid().ToString("N");
            Count++;
        }
    }

    //collection A

    [CollectionDefinition(Name)]
    public class CollectionA : TestCollection<CollectionAppFixture>
    {
        public const string Name = nameof(CollectionA);
    }

    [Collection(CollectionA.Name)]
    public class TestClassA1(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_1()
        {
            App.Id.Should().Be(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.Should().Be(1);
        }
    }

    [Collection(CollectionA.Name)]
    public class TestClassA2(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_2()
        {
            App.Id.Should().Be(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.Should().Be(1);
        }
    }

    //collection B

    [CollectionDefinition(Name)]
    public class CollectionB : TestCollection<CollectionAppFixture>
    {
        public const string Name = nameof(CollectionB);
    }

    [Collection(CollectionB.Name)]
    public class TestClassB1(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_1()
        {
            App.Id.Should().Be(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.Should().Be(1);
        }
    }

    [Collection(CollectionB.Name)]
    public class TestClassB2(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_2()
        {
            App.Id.Should().Be(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.Should().Be(1);
        }
    }
}