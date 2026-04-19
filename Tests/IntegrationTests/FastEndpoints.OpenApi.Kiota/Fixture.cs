namespace OpenApi.Kiota.Tests;

public sealed class Fixture : AppFixture<Program>
{
    protected override ValueTask SetupAsync()
    {
        Client = CreateClient();

        return ValueTask.CompletedTask;
    }
}