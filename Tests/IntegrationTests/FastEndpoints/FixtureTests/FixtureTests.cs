using FakeItEasy;

namespace TestFixture;

public class FixtureTests
{
    [Fact]
    public async Task Only_One_TestServer_Is_Created_Per_Fixture_Type()
    {
        var sink = A.Fake<IMessageSink>();

        var f1 = new FixtureOne(sink);
        var f2 = new FixtureOne(sink);

        var id1 = f1.Services.GetRequiredService<FixtureId>();
        var id2 = f2.Services.GetRequiredService<FixtureId>();

        id1.Should().Be(id2);

        var t1 = new FixtureTwo(sink);
        var t2 = new FixtureTwo(sink);

        var tid1 = t1.Services.GetRequiredService<FixtureId>();
        var tid2 = t2.Services.GetRequiredService<FixtureId>();

        tid1.Should().Be(tid2);
    }
}