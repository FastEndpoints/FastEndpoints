namespace TestCases.ServiceRegistrationGeneratorTest;

[RegisterService<AScopedService>(LifeTime.Scoped)]
public class AScopedService
{
    public string Type { get; set; } = "Scoped";
}

[RegisterService<ATransientService>(LifeTime.Transient)]
public class ATransientService
{
    public string Type { get; set; } = "Transient";
}

[RegisterService<ASingletonService>(LifeTime.Singleton)]
public class ASingletonService
{
    public string Type { get; set; } = "Singleton";
}

sealed class Endpoint : EndpointWithoutRequest<string[]>
{
    public AScopedService ScopedService { get; set; }
    public ATransientService TransientService { get; set; }
    public ASingletonService SingletonService { get; set; }

    public override void Configure()
    {
        Get("/test-cases/service-reg-gen-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await SendAsync(new[] { ScopedService.Type, TransientService.Type, SingletonService.Type });
    }
}