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
        Version(1).DeprecateAt(2);
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
        Version(2)
            .StartingRelease(3)
            .DeprecateAt(4);
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

sealed class EndpointB_V1_Delete : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/release-versioning/endpoint-b");
        Tags("release_versioning");
        Version(1).StartingRelease(2);
    }

    public override Task HandleAsync(CancellationToken c)
        => Task.CompletedTask;
}