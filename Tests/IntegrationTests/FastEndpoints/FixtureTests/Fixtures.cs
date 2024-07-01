using Messaging;
using Microsoft.Extensions.Hosting;
using TestCases.CommandBusTest;
using Web;
using Web.Services;

namespace FixtureTests;

public class FixtureA(IMessageSink s) : AppFixture<Web.Program>(s)
{
    static readonly string _id = Guid.NewGuid().ToString("N");

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.AddScoped<IEmailService, MockEmailService>();
        s.RegisterTestCommandHandler<SomeCommand, TestCommandHandler, string>();
        s.AddSingleton(new FixtureId(_id));
    }
}

public class FixtureB(IMessageSink s) : AppFixture<Web.Program>(s)
{
    static readonly string _id = Guid.NewGuid().ToString("N");

    protected override void ConfigureServices(IServiceCollection s)
    {
        s.RegisterTestCommandHandler<SomeCommand, TestCommandHandler, string>();
        s.AddSingleton(new FixtureId(_id));
    }
}

public class FixtureWithConfigureAppHost(IMessageSink s) : AppFixture<Web.Program>(s)
{
    internal static readonly string Id = Guid.NewGuid().ToString("N");
    internal IHost Host = null!;

    protected override IHost ConfigureAppHost(IHostBuilder a)
    {
        a.ConfigureServices(services => { services.AddSingleton(new FixtureId(Id)); });
        Host = a.Build();
        Host.Start();

        return Host;
    }
}