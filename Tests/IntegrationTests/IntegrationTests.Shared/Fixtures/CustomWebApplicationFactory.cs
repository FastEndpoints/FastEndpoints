using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace IntegrationTests.Shared.Fixtures;

public class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    public ITestOutputHelper? OutputHelper { get; set; }
    public Action<IServiceCollection>? TestRegistrationServices { get; set; }

    public CustomWebApplicationFactory(Action<IServiceCollection>? testRegistrationServices = null)
    {
        TestRegistrationServices = testRegistrationServices ?? (collection => { });
    }

    //https://github.com/dotnet/aspnetcore/issues/17707
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // Remove other loggers
            if (OutputHelper is { })
                logging.AddXUnit(OutputHelper); // Use the ITestOutputHelper instance
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        //https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests#set-the-environment
        //https://stackoverflow.com/questions/43927955/should-getenvironmentvariable-work-in-xunit-test/43951218

        // //we could read env from our test launch setting or we can set it directly here
        builder.UseEnvironment("test");

        //The test app's builder.ConfigureTestServices callback is executed after the app's Startup.ConfigureServices code is executed.
        builder.ConfigureTestServices((services) =>
        {
            services.RemoveAll(typeof(IHostedService));

            services.AddHttpContextAccessor();

            TestRegistrationServices?.Invoke(services);
        });

        //The test app's builder.ConfigureServices callback is executed before the SUT's Startup.ConfigureServices code.
        builder.ConfigureServices(services => { });

        builder.UseDefaultServiceProvider((env, c) =>
        {
            // Handling Captive Dependency Problem
            // https://ankitvijay.net/2020/03/17/net-core-and-di-beware-of-captive-dependency/
            // https://blog.ploeh.dk/2014/06/02/captive-dependency/
            if (env.HostingEnvironment.IsEnvironment("test") || env.HostingEnvironment.IsDevelopment())
                c.ValidateScopes = true;
        });
    }
}