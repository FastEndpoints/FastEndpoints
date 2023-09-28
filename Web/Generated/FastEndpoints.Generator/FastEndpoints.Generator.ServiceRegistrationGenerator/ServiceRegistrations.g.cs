namespace Web;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection RegisterServicesFromWeb(this IServiceCollection sc)
    {
        sc.AddScoped<TestCases.ServiceRegistrationGeneratorTest.AScopedService, TestCases.ServiceRegistrationGeneratorTest.AScopedService>();
        sc.AddSingleton<TestCases.ServiceRegistrationGeneratorTest.ASingletonService, TestCases.ServiceRegistrationGeneratorTest.ASingletonService>();
        sc.AddTransient<TestCases.ServiceRegistrationGeneratorTest.ATransientService, TestCases.ServiceRegistrationGeneratorTest.ATransientService>();

        return sc;
    }
}