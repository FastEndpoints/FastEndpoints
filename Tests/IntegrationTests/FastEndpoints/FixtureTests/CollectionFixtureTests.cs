namespace FixtureTests;

public class CollectionFixtureTests
{
    // collection fixture

    public class CollectionAppFixture : AppFixture<Web.Program>
    {
        public static string Default { get; } = Guid.NewGuid().ToString("N");
        public string Id { get; private set; } = null!;
        public int Count { get; private set; }

        protected override async ValueTask SetupAsync()
        {
            //proves setup was run
            Id = Default.Reverse().ToString()!;
            Count++;
        }

        protected override async ValueTask TearDownAsync()
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
            App.Id.ShouldBe(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.ShouldBe(1);
        }
    }

    [Collection(CollectionA.Name)]
    public class TestClassA2(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_2()
        {
            App.Id.ShouldBe(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.ShouldBe(1);
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
            App.Id.ShouldBe(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.ShouldBe(1);
        }
    }

    [Collection(CollectionB.Name)]
    public class TestClassB2(CollectionAppFixture App) : TestBase
    {
        [Fact]
        public void Test_Method_2()
        {
            App.Id.ShouldBe(CollectionAppFixture.Default.Reverse().ToString());
            App.Count.ShouldBe(1);
        }
    }
}