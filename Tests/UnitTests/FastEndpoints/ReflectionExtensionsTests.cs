using System.Linq.Expressions;
using FastEndpoints;
using Xunit;

namespace ReflectionExtensions;

public class PropNamesTests
{
    [Fact]
    public void SingleLevelMemberAccessReturnsPropertyNames()
    {
        Expression<Func<Request, object>> expr = x => new { x.Id, x.Name };

        var names = expr.PropNames().ToArray();

        names.ShouldBe(["Id", "Name"]);
    }

    [Fact]
    public void BindFromAttributeOverridesPropertyName()
    {
        Expression<Func<Request, object>> expr = x => new { x.Aliased };

        var names = expr.PropNames().ToArray();

        names.ShouldBe(["custom_name"]);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class Request
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        [BindFrom("custom_name")]
        public string Aliased { get; set; } = "";
    }
}
