using Xunit;

namespace Shared;

public abstract class TestBase : IClassFixture<AppFixture>
{
    protected AppFixture App { get; init; }

    protected TestBase(AppFixture fixture)
    {
        App = fixture;
    }
}