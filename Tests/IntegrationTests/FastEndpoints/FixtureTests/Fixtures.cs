namespace TestFixture;

public record FixtureId(string Id);

public class FixtureOne : TestFixture<Web.Program>
{
    public FixtureOne(IMessageSink s) : base(s) { }

    private static readonly string _id = Guid.NewGuid().ToString("N");

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.AddSingleton(new FixtureId(_id));
    }
}

public class FixtureTwo : TestFixture<Web.Program>
{
    public FixtureTwo(IMessageSink s) : base(s) { }

    private static string _id = Guid.NewGuid().ToString("N");

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.AddSingleton(new FixtureId(_id));
    }
}