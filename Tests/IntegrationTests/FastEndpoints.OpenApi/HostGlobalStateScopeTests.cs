using System.Text.Json;
using FakeItEasy;
using FastEndpoints;
using FluentValidation;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class HostGlobalStateScopeTests
{
    [Fact]
    public void globals_are_restored_when_the_protected_lifetime_throws()
    {
        var previousResolver = MainExtensions.HostServiceResolver;
        var previousSerializerConfigured = MainExtensions.SerializerConfigured;
        var previousSerializerOptions = Config.SerOpts.Options;
        var previousNameResolver = ValidatorOptions.Global.PropertyNameResolver;
        var replacementResolver = A.Fake<IServiceResolver>();
        var replacementSerializerOptions = new JsonSerializerOptions();

        Should.Throw<InvalidOperationException>(() =>
        {
            using var globalState = new HostGlobalStateScope();
            MainExtensions.HostServiceResolver = replacementResolver;
            MainExtensions.SerializerConfigured = !previousSerializerConfigured;
            Config.SerOpts.Options = replacementSerializerOptions;
            ValidatorOptions.Global.PropertyNameResolver = (_, _, _) => "replacement";

            throw new InvalidOperationException("Protected lifetime failed.");
        });

        MainExtensions.HostServiceResolver.ShouldBeSameAs(previousResolver);
        MainExtensions.SerializerConfigured.ShouldBe(previousSerializerConfigured);
        Config.SerOpts.Options.ShouldBeSameAs(previousSerializerOptions);
        ValidatorOptions.Global.PropertyNameResolver.ShouldBeSameAs(previousNameResolver);
    }
}
