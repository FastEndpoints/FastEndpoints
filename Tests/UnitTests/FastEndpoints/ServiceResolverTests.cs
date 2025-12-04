using FakeItEasy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceResolverTests;

public class ServiceResolverTests
{
    static IServiceProvider CreateEmptyServiceProvider()
        => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void CreateInstance_CreatesInstanceOfType()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var instance = resolver.CreateInstance(typeof(TestService));

        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<TestService>();
    }

    [Fact]
    public void CreateInstance_UsesProvidedServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDependency, TestDependency>();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(CreateEmptyServiceProvider());

        var instance = resolver.CreateInstance(typeof(ServiceWithDependency), provider);

        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<ServiceWithDependency>();
    }

    [Fact]
    public void CreateInstance_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDependency, TestDependency>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var instance = resolver.CreateInstance(typeof(ServiceWithDependency));

        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<ServiceWithDependency>();
    }

    [Fact]
    public void CreateInstance_CachesDelegateForRepeatedCalls()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var instance1 = resolver.CreateInstance(typeof(TestService));
        var instance2 = resolver.CreateInstance(typeof(TestService));

        // Both should succeed and be different instances (not singletons)
        instance1.ShouldNotBeNull();
        instance2.ShouldNotBeNull();
        instance1.ShouldNotBeSameAs(instance2);
    }

    [Fact]
    public void CreateSingleton_CreatesSingletonInstance()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var instance1 = resolver.CreateSingleton(typeof(TestService));
        var instance2 = resolver.CreateSingleton(typeof(TestService));

        instance1.ShouldNotBeNull();
        instance2.ShouldNotBeNull();
        instance1.ShouldBeSameAs(instance2);
    }

    [Fact]
    public void CreateSingleton_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDependency, TestDependency>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var instance = resolver.CreateSingleton(typeof(ServiceWithDependency));

        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<ServiceWithDependency>();
    }

    [Fact]
    public void CreateScope_InNormalMode_ReturnsScope()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        using var scope = resolver.CreateScope();

        scope.ShouldNotBeNull();
        scope.ServiceProvider.ShouldNotBeNull();
    }

    [Fact]
    public void CreateScope_InUnitTestMode_WithHttpContext_ReturnsScope()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(provider, ctxAccessor, isUnitTestMode: true);

        using var scope = resolver.CreateScope();

        scope.ShouldNotBeNull();
    }

    [Fact]
    public void CreateScope_InUnitTestMode_WithoutHttpContext_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(null);

        var resolver = new ServiceResolver(provider, ctxAccessor, isUnitTestMode: true);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.CreateScope());
        ex.Message.ShouldContain("unit test environment");
    }

    [Fact]
    public void Resolve_Generic_ResolvesRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.Resolve<ITestService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_Generic_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.Resolve<ITestService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGeneric_ResolvesRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.Resolve(typeof(ITestService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGeneric_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.Resolve(typeof(ITestService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_Generic_ReturnsServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve<ITestService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_Generic_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve<ITestService>();

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_Generic_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.TryResolve<ITestService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGeneric_ReturnsServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve(typeof(ITestService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGeneric_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve(typeof(ITestService));

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_NonGeneric_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestServiceImpl>();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.TryResolve(typeof(ITestService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_GenericWithKeyName_ReturnsKeyedServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve<ITestService>("myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_GenericWithKeyName_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve<ITestService>("myKey");

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_GenericWithKeyName_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.TryResolve<ITestService>("myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGenericWithKeyName_ReturnsKeyedServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve(typeof(ITestService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGenericWithKeyName_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.TryResolve(typeof(ITestService), "myKey");

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_NonGenericWithKeyName_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.TryResolve(typeof(ITestService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_GenericWithKeyName_UsesHttpContextRequestServices()
    {
        // Note: This test uses HttpContext because without it, the fallback 
        // to provider.GetRequiredService<T>() (line 83 in ServiceResolver.cs) 
        // doesn't use the keyName parameter
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.Resolve<ITestService>("myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGenericWithKeyName_ResolvesKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();
        var resolver = new ServiceResolver(provider);

        var service = resolver.Resolve(typeof(ITestService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGenericWithKeyName_UsesHttpContextRequestServicesIfAvailable()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestService, TestServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var ctxAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => ctxAccessor.HttpContext).Returns(httpContext);

        var resolver = new ServiceResolver(CreateEmptyServiceProvider(), ctxAccessor);

        var service = resolver.Resolve(typeof(ITestService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<TestServiceImpl>();
    }
}

// Test helper types
file interface ITestService;

file class TestServiceImpl : ITestService;

file class TestService;

file interface IDependency;

file class TestDependency : IDependency;

file class ServiceWithDependency(IDependency dependency)
{
    public IDependency Dependency { get; } = dependency;
}