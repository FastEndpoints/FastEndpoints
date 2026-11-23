using FastEndpoints;
using FastEndpoints.Generator;
using Xunit;

namespace Generator;

public class ServiceRegistrationGeneratorTests
{
    [Fact]
    public void lifetimes_and_attribute_forms_are_resolved_semantically()
    {
        const string source =
            """
            using FastEndpoints;
            using static FastEndpoints.LifeTime;

            namespace TestApp;

            public interface IFoo;
            public interface IBar;
            public interface IBaz;
            public interface IAbstract;

            [RegisterService<IFoo>(Scoped)]
            public class Foo : IFoo;

            [FastEndpoints.RegisterService<IBar>(LifeTime.Singleton)]
            public record Bar : IBar;

            [RegisterServiceAttribute<IBaz>(serviceLifetime: (LifeTime)1)]
            public class Baz : IBaz;

            [RegisterService<IAbstract>(Transient)]
            public abstract class Abstract : IAbstract;
            """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>(
            [
                source,
                "namespace TestApp.Microsoft { public class Marker; }" //must not shadow the generated using
            ],
            GeneratorTestHarness.GetReferences(typeof(RegisterServiceAttribute<>).Assembly));

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("sc.AddScoped<global::TestApp.IFoo, global::TestApp.Foo>();");
        result.Generated.ShouldContain("sc.AddSingleton<global::TestApp.IBar, global::TestApp.Bar>();");
        result.Generated.ShouldContain("sc.AddTransient<global::TestApp.IBaz, global::TestApp.Baz>();");
        result.Generated.ShouldNotContain("Abstract");
    }
}
