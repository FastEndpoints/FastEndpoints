// using Tests.Fixtures;
//
// public class MyTests(Sut App) : TestBase<Sut>
// {
//     [Fact]
//     public async Task Query_Param_Test() { }
// }

using Tests.Fixtures;

// ReSharper disable InconsistentNaming

[CollectionDefinition(A), Priority(1)] //ordering collections
public class Collection_A : TestCollection<Sut>
{
    const string A = nameof(Collection_A);

    [Collection(A), Priority(2)] //ordering classes within a collection
    public class Second_Class(Sut App) : TestBase
    {
        [Fact, Priority(2)] //ordering tests within the class
        public Task Fourth()
            => Task.CompletedTask;

        [Fact, Priority(1)]
        public Task Third()
            => Task.CompletedTask;
    }

    [Collection(A), Priority(1)]
    public class First_Class(Sut App) : TestBase
    {
        [Fact, Priority(1)]
        public Task First() //this test method is executed first
            => Task.CompletedTask;

        [Fact, Priority(2)]
        public Task Second()
            => Task.CompletedTask;
    }
}

[CollectionDefinition(B), Priority(2)]
public class Collection_B : TestCollection<Sut>
{
    const string B = nameof(Collection_B);

    [Collection(B), Priority(2)]
    public class Second_Class(Sut App) : TestBase
    {
        [Fact, Priority(2)]
        public Task Eighth() //this test method is executed last
            => Task.CompletedTask;

        [Fact, Priority(1)]
        public Task Seventh()
            => Task.CompletedTask;
    }

    [Collection(B), Priority(1)]
    public class First_Class(Sut App) : TestBase
    {
        [Fact, Priority(1)]
        public Task Fifth()
            => Task.CompletedTask;

        [Fact, Priority(2)]
        public Task Sixth()
            => Task.CompletedTask;
    }
}