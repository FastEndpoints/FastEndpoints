using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class ServiceRegistrationGeneratorTests
{
    static readonly IEnumerable<Microsoft.CodeAnalysis.MetadataReference> _references =
        GeneratorTestHarness.GetReferences(typeof(RegisterServiceAttribute<>).Assembly);

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

    [Fact]
    public void obsolete_types_do_not_raise_obsolete_warnings_in_generated_code()
    {
        const string source =
            """
            using System;
            using FastEndpoints;

            namespace TestApp;

            public interface IFoo;
            public interface IBar;

            [Obsolete, RegisterService<IFoo>(LifeTime.Scoped)]
            public class Foo : IFoo;

            [Obsolete("use something else"), RegisterService<IBar>(LifeTime.Scoped)]
            public class Bar : IBar;
            """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([source], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("sc.AddScoped<global::TestApp.IFoo, global::TestApp.Foo>();");
        result.Generated.ShouldContain("sc.AddScoped<global::TestApp.IBar, global::TestApp.Bar>();");
        GeneratorTestHarness.AssertNoObsoleteWarningsInGeneratedCode(result);
    }


    [Fact]
    public void generic_types_are_skipped_and_nested_types_are_fully_qualified()
    {
        const string source =
            """
            using FastEndpoints;

            namespace TestApp;

            public interface IFoo;
            public interface IBar;
            public interface IBaz;

            public class Outer
            {
                [RegisterService<IFoo>(LifeTime.Scoped)]
                public class Inner : IFoo;
            }

            public class GenericOuter<T>
            {
                [RegisterService<IBar>(LifeTime.Scoped)]
                public class NestedInGeneric : IBar;
            }

            [RegisterService<IBaz>(LifeTime.Scoped)]
            public class OpenGeneric<T> : IBaz;
            """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([source], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("sc.AddScoped<global::TestApp.IFoo, global::TestApp.Outer.Inner>();");
        result.Generated.ShouldNotContain("NestedInGeneric");
        result.Generated.ShouldNotContain("OpenGeneric");
    }

    [Theory]
    [InlineData("public class Foo : IFoo;")]                                      //no attribute
    [InlineData("[RegisterService<IFoo>] public class Foo : IFoo;")]              //missing lifetime (code being typed)
    [InlineData("[RegisterService<IFoo>((LifeTime)5)] public class Foo : IFoo;")] //undefined lifetime
    public void no_file_is_generated_without_a_valid_registration(string declaration)
    {
        var source =
            $"""
             using FastEndpoints;

             namespace TestApp;

             public interface IFoo;

             {declaration}
             """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([source], _references);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedTreeCount.ShouldBe(0);
    }

    [Fact]
    public void assembly_name_is_turned_into_a_valid_namespace_and_method_name()
    {
        const string source =
            """
            using FastEndpoints;

            namespace TestApp;

            public interface IFoo;

            [RegisterService<IFoo>(LifeTime.Scoped)]
            public class Foo : IFoo;
            """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([source], _references, assemblyName: "My.App-1");

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("namespace My.App_1;");
        result.Generated.ShouldContain("RegisterServicesFromMyApp1(");
    }

    [Fact]
    public void registrations_are_ordered_by_lifetime_service_and_implementation_regardless_of_file_order()
    {
        const string first =
            """
            using FastEndpoints;

            namespace TestApp;

            public interface IFoo;
            public interface IBar;

            [RegisterService<IFoo>(LifeTime.Transient)]
            public class TransientFoo : IFoo;

            [RegisterService<IFoo>(LifeTime.Scoped)]
            public class FooB : IFoo;

            [RegisterService<IBar>(LifeTime.Scoped)]
            public class Bar : IBar;
            """;
        const string second =
            """
            using FastEndpoints;

            namespace TestApp;

            [RegisterService<IFoo>(LifeTime.Scoped)]
            public class FooA : IFoo;

            [RegisterService<IFoo>(LifeTime.Singleton)]
            public class SingletonFoo : IFoo;
            """;

        var result = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([first, second], _references);
        var reversed = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([second, first], _references);

        GeneratorTestHarness.AssertCompiles(result);
        reversed.Generated.ShouldBe(result.Generated);

        string[] expectedOrder =
        [
            "sc.AddScoped<global::TestApp.IBar, global::TestApp.Bar>();",
            "sc.AddScoped<global::TestApp.IFoo, global::TestApp.FooA>();",
            "sc.AddScoped<global::TestApp.IFoo, global::TestApp.FooB>();",
            "sc.AddSingleton<global::TestApp.IFoo, global::TestApp.SingletonFoo>();",
            "sc.AddTransient<global::TestApp.IFoo, global::TestApp.TransientFoo>();"
        ];
        var positions = expectedOrder.Select(r => result.Generated.IndexOf(r, StringComparison.Ordinal)).ToArray();

        positions.ShouldAllBe(p => p >= 0);
        positions.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void changed_registrations_are_picked_up_by_a_reused_driver()
    {
        static string Source(string lifeTime)
            => $$"""
                 using FastEndpoints;

                 namespace TestApp;

                 public interface IFoo;

                 [RegisterService<IFoo>(LifeTime.{{lifeTime}})]
                 public class Foo : IFoo;
                 """;

        var first = GeneratorTestHarness.Run<ServiceRegistrationGenerator>([Source("Scoped")], _references);
        var second = GeneratorTestHarness.Step(
            first.Driver,
            first.Input.ReplaceSyntaxTree(
                first.Input.SyntaxTrees.Single(),
                CSharpSyntaxTree.ParseText(Source("Singleton"), GeneratorTestHarness.DefaultParseOptions)));

        first.Generated.ShouldContain("sc.AddScoped<global::TestApp.IFoo, global::TestApp.Foo>();");
        GeneratorTestHarness.AssertCompiles(second);
        second.Generated.ShouldContain("sc.AddSingleton<global::TestApp.IFoo, global::TestApp.Foo>();");
        second.Generated.ShouldNotContain("AddScoped");
    }
}
