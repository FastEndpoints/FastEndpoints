// ReSharper disable InconsistentNaming

namespace TestCases.Swagger.ReleaseVersioning;

sealed class EndpointA : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/release-versioning/endpoint-a");
        Tags("release_versioning");
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}

sealed class EndpointA_V1 : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/release-versioning/endpoint-a");
        Tags("release_versioning");
        Version(1, deprecateAt: 2);
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}

sealed class EndpointA_V2 : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/release-versioning/endpoint-a");
        Tags("release_versioning");
        Version(2).StartingRelease(3);
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}

sealed class EndpointB : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/release-versioning/endpoint-b");
        Tags("release_versioning");
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}

sealed class EndpointB_V1 : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/release-versioning/endpoint-b");
        Tags("release_versioning");
        Version(1).StartingRelease(2);
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}