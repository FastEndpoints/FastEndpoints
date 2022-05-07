using Microsoft.Extensions.Configuration;

namespace IntegrationTests.Shared.Utilities;

public static class ConfigurationHelper
{
    //https://www.thecodebuzz.com/read-appsettings-json-in-net-core-test-project-xunit-mstest/
    //https://weblog.west-wind.com/posts/2018/Feb/18/Accessing-Configuration-in-NET-Core-Test-Projects
    //https://bartwullems.blogspot.com/2019/03/net-coreunit-tests-configuration.html
    public static IConfigurationRoot BuildConfiguration(string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.test.json")
            .AddEnvironmentVariables()
            .Build();
    }
}