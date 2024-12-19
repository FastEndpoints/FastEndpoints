// using Tests.Fixtures;
//
// public class MyTests(Sut App) : TestBase<Sut>
// {
//     [Fact]
//     public async Task Query_Param_Test() { }
// }

using Tests.Fixtures;

// ReSharper disable ArrangeAttributes
// ReSharper disable InconsistentNaming

[Priority(1)]                                    //ordering at collection level
public class Collection_A : TestCollection<Sut>; //define collection A

[Priority(2)]                                    //ordering at collection level
public class Collection_B : TestCollection<Sut>; //define collection B

[Collection<Collection_A>] //associate class with collection A
[Priority(1)]              //ordering at class level
public class A_First_Class(Sut App) : TestBase
{
    [Fact, Priority(1)] //ordering at case level
    public Task First() //this case is executed first
        => Task.CompletedTask;

    [Fact, Priority(2)] //ordering at case level
    public Task Second()
        => Task.CompletedTask;
}

[Collection<Collection_A>] //associate class with collection A
[Priority(2)]              //ordering at class level
public class A_Second_Class(Sut App) : TestBase
{
    [Fact, Priority(2)]
    public Task Fourth()
        => Task.CompletedTask;

    [Fact, Priority(1)]
    public Task Third()
        => Task.CompletedTask;
}

[Collection<Collection_B>] //associate class with collection B
[Priority(2)]              //ordering at class level
public class B_Second_Class(Sut App) : TestBase
{
    [Fact, Priority(2)]
    public Task Eighth() //this case is executed last
        => Task.CompletedTask;

    [Fact, Priority(1)]
    public Task Seventh()
        => Task.CompletedTask;
}

[Collection<Collection_B>] //associate class with collection B
[Priority(1)]              //ordering at class level
public class B_First_Class(Sut App) : TestBase
{
    [Fact, Priority(1)]
    public Task Fifth()
        => Task.CompletedTask;

    [Fact, Priority(2)]
    public Task Sixth()
        => Task.CompletedTask;
}