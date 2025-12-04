using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceScopeExtensionsTests;

public class ServiceScopeExtensionsTests
{
    [Fact]
    public void TryResolve_Generic_ReturnsServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve<IScopedService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void TryResolve_Generic_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve<IScopedService>();

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_NonGeneric_ReturnsServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve(typeof(IScopedService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGeneric_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve(typeof(IScopedService));

        service.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Generic_ResolvesRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.Resolve<IScopedService>();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void Resolve_Generic_ThrowsWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Resolve<IScopedService>());
    }

    [Fact]
    public void Resolve_NonGeneric_ResolvesRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.Resolve(typeof(IScopedService));

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGeneric_ThrowsWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Resolve(typeof(IScopedService)));
    }

    [Fact]
    public void TryResolve_GenericWithKeyName_ReturnsKeyedServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IScopedService, ScopedServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve<IScopedService>("myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void TryResolve_GenericWithKeyName_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve<IScopedService>("myKey");

        service.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_NonGenericWithKeyName_ReturnsKeyedServiceWhenRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IScopedService, ScopedServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve(typeof(IScopedService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void TryResolve_NonGenericWithKeyName_ReturnsNullWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.TryResolve(typeof(IScopedService), "myKey");

        service.ShouldBeNull();
    }

    [Fact]
    public void Resolve_GenericWithKeyName_ResolvesKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IScopedService, ScopedServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.Resolve<IScopedService>("myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void Resolve_GenericWithKeyName_ThrowsWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Resolve<IScopedService>("myKey"));
    }

    [Fact]
    public void Resolve_NonGenericWithKeyName_ResolvesKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IScopedService, ScopedServiceImpl>("myKey");
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service = scope.Resolve(typeof(IScopedService), "myKey");

        service.ShouldNotBeNull();
        service.ShouldBeOfType<ScopedServiceImpl>();
    }

    [Fact]
    public void Resolve_NonGenericWithKeyName_ThrowsWhenNotRegistered()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.Resolve(typeof(IScopedService), "myKey"));
    }

    [Fact]
    public void ScopedService_ReturnsSameInstanceWithinScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var service1 = scope.Resolve<IScopedService>();
        var service2 = scope.Resolve<IScopedService>();

        service1.ShouldBeSameAs(service2);
    }

    [Fact]
    public void ScopedService_ReturnsDifferentInstancesAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedServiceImpl>();
        var provider = services.BuildServiceProvider();

        IScopedService service1;
        IScopedService service2;

        using (var scope1 = provider.CreateScope())
            service1 = scope1.Resolve<IScopedService>();

        using (var scope2 = provider.CreateScope())
            service2 = scope2.Resolve<IScopedService>();

        service1.ShouldNotBeSameAs(service2);
    }
}

// Test helper types
file interface IScopedService;

file class ScopedServiceImpl : IScopedService;