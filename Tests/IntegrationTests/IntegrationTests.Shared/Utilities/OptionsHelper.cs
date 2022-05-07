using Microsoft.Extensions.Configuration;

namespace IntegrationTests.Shared.Utilities;

public static class OptionsHelper
{
    public static TSettings GetOptions<TSettings>(string section, string? settingsFileName = null)
        where TSettings : class, new()
    {
        var configuration = new TSettings();

        ConfigurationHelper.BuildConfiguration(settingsFileName)
            .GetSection(section)
            .Bind(configuration);

        return configuration;
    }
}
