using FakeItEasy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusIsolationScopeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exceptional_Exit_Restores_Preexisting_Context_And_Resolver(bool resolverInitiallySet)
    {
        var originalResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
        var accessor = new HttpContextAccessor();
        var originalContext = accessor.HttpContext;

        try
        {
            var previousResolver = resolverInitiallySet ? A.Fake<IServiceResolver>() : null;
            var previousContext = new DefaultHttpContext();
            ServiceResolver.Instance = previousResolver!;
            accessor.HttpContext = previousContext;

            Should.Throw<InvalidOperationException>(() =>
            {
                using var scope = new EventBusIsolationScope();
                accessor.HttpContext.ShouldBeNull();
                if (resolverInitiallySet)
                    ServiceResolver.Instance.ShouldBeSameAs(previousResolver);
                else
                    ServiceResolver.InstanceNotSet.ShouldBeTrue();

                ServiceResolver.Instance = A.Fake<IServiceResolver>();
                accessor.HttpContext = new DefaultHttpContext();
                throw new InvalidOperationException();
            });

            accessor.HttpContext.ShouldBeSameAs(previousContext);
            if (resolverInitiallySet)
                ServiceResolver.Instance.ShouldBeSameAs(previousResolver);
            else
                ServiceResolver.InstanceNotSet.ShouldBeTrue();
        }
        finally
        {
            ServiceResolver.Instance = originalResolver!;
            accessor.HttpContext = originalContext;
        }
    }
}
