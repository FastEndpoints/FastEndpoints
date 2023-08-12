using Xunit;

namespace Shared;

public abstract class TestBase : IClassFixture<WebFixture>
{
    protected WebFixture Web { get; init; }

    protected TestBase(WebFixture fixture)
    {
        Web = fixture;
    }
}