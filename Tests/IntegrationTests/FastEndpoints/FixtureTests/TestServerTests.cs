namespace FixtureTests;

public record FixtureId(string Id);

public class TestServerTests
{
    [Fact]
    public async Task Only_One_TestServer_Is_Created_Per_Fixture_Type()
    {
        var f1 = new FixtureA();
        var f2 = new FixtureA();

        await ((IAsyncLifetime)f1).InitializeAsync();
        await ((IAsyncLifetime)f2).InitializeAsync();

        var id1 = f1.Services.GetRequiredService<FixtureId>();
        var id2 = f2.Services.GetRequiredService<FixtureId>();

        id1.ShouldBe(id2);

        var t1 = new FixtureB();
        var t2 = new FixtureB();

        await ((IAsyncLifetime)t1).InitializeAsync();
        await ((IAsyncLifetime)t2).InitializeAsync();

        var tid1 = t1.Services.GetRequiredService<FixtureId>();
        var tid2 = t2.Services.GetRequiredService<FixtureId>();

        tid1.ShouldBe(tid2);
    }
}