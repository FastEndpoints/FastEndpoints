using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class ReflectionGeneratorTests
{
    [Fact]
    public void request_dto_property_emits_getter_delegate()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyRequest
            {
                public string Name { get; set; } = string.Empty;
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        generated.ShouldContain("Setter = (dto, val) => ((t0)dto).Name = (string)val!");
    }

    [Fact]
    public void init_only_request_dto_property_emits_getter_without_setter()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyRequest
            {
                public string Name { get; init; } = string.Empty;
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
        generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        generated.ShouldNotContain("Setter =");
    }

    [Fact]
    public void endpoint_without_request_keyed_service_property_is_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("A")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"A\"");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void endpoint_with_propertyless_dto_keyed_service_is_emitted()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class EmptyDto { }

            public class MyEndpoint : Endpoint<EmptyDto, string>
            {
                [KeyedService("B")]
                public object MyService { get; set; } = default!;

                public override void Configure() { }

                public override Task HandleAsync(EmptyDto req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"B\"");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void keyed_init_property_emits_service_key_without_setter()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("KEY")]
                public object Service { get; init; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("ServiceKey = \"KEY\"");
        generated.ShouldNotContain("Setter =");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void service_key_with_embedded_quote_is_escaped_in_generated_code()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\"a")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        // FormatLiteral emits the verbatim C# string literal, e.g. "tenant\"a"
        generated.ShouldContain(@"ServiceKey = ""tenant\""a""");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void service_key_with_backslash_is_escaped_in_generated_code()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\\path")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldContain(@"ServiceKey = ""tenant\\path""");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void generated_code_with_escaped_key_compiles_without_errors()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest<string>
            {
                [KeyedService("tenant\"a")]
                public object MyService { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        RunGenerator(source, out var diagnostics, out var outputCompilation);

        diagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics()
                         .Where(d => d.Severity == DiagnosticSeverity.Error)
                         .ShouldBeEmpty();
    }

    [Fact]
    public void unrelated_keyed_service_attribute_is_ignored()
    {
        const string source =
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace Other
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class KeyedServiceAttribute(string keyName) : Attribute;
            }

            namespace TestApp
            {
                public class MyEndpoint : EndpointWithoutRequest<string>
                {
                    [Other.KeyedService("NOT_FASTENDPOINTS")]
                    public object MyService { get; set; } = default!;

                    public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
                }
            }
            """;

        var generated = RunGenerator(source, out var diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.ShouldNotContain("NOT_FASTENDPOINTS");
        generated.ShouldContain("MyEndpoint");
    }

    [Fact]
    public void empty_source_emits_empty_reflection_registration()
    {
        var result = Run("");

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
    }

    [Fact]
    public void messaging_only_consumer_does_not_require_library_reflection_cache()
    {
        var result = GeneratorTestHarness.Run<ReflectionGenerator>(
            ["public sealed class Message;"],
            _references.Where(static r => r.Display != typeof(ReflectionCache).Assembly.Location),
            assemblyName: "MessagingOnly");

        result.GeneratedTreeCount.ShouldBe(0);
        GeneratorTestHarness.AssertCompiles(result);
    }

    [Fact]
    public void record_only_source_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
    }

    [Fact]
    public void generic_class_only_source_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public class Request<T>
            {
                public T Value { get; set; } = default!;
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
    }

    [Fact]
    public void class_without_endpoint_emits_empty_reflection_registration()
    {
        const string source =
            """
            namespace MyApp;

            public class Request
            {
                public string Name { get; set; } = "";
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("namespace MyApp;");
        result.Generated.ShouldNotContain("Getter =");
    }

    [Fact]
    public void response_alias_endpoint_emits_request_property_accessors()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            using CreateSessionResponse = Microsoft.AspNetCore.Http.HttpResults.Results<
                Microsoft.AspNetCore.Http.HttpResults.Created<MyApp.CreateSessionApiResponse>,
                Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>;

            namespace MyApp;

            public class CreateSessionApiResponse
            {
                public string Id { get; set; } = "";
            }

            public class CreateSessionRequest
            {
                public string Name { get; set; } = "";
            }

            public class CreateSessionEndpoint : Endpoint<CreateSessionRequest, CreateSessionResponse>
            {
                public override void Configure() { }

                public override Task HandleAsync(CreateSessionRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: false);
        result.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        result.Generated.ShouldContain("Setter = (dto, val) => ((t0)dto).Name = (string)val!");
        result.Generated.ShouldContain("CreateSessionRequest");
    }

    [Fact]
    public void assembly_name_is_sanitized_for_namespace_and_registration_method()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source, "My-App.Host");

        AssertRegistration(result, "My_App.Host", "MyAppHost", empty: true);
    }

    [Fact]
    public void null_assembly_name_falls_back_to_assembly()
    {
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var result = Run(source, null);

        AssertRegistration(result, "Assembly", "Assembly", empty: true);
    }

    [Fact]
    public void reused_driver_tracks_assembly_name_without_syntax_transform()
    {
        // Records are not class declarations, so Transform never runs. The namespace still has to follow the compilation.
        const string source =
            """
            namespace MyApp;

            public record Request(string Name);
            """;

        var first = Run(source, "First.App");
        AssertRegistration(first, "First.App", "FirstApp", empty: true);

        var second = Execute(first.Driver, first.Input.WithAssemblyName("Second-App"));
        AssertRegistration(second, "Second_App", "SecondApp", empty: true);
        second.Generated.ShouldNotContain("namespace First.App;");
        second.Generated.ShouldNotContain("AddFromFirstApp");
    }

    [Fact]
    public void reused_driver_keeps_request_accessors_when_assembly_name_changes()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                public string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<MyRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var first = Run(source, "First.App");
        AssertRegistration(first, "First.App", "FirstApp", empty: false);
        first.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");

        var second = Execute(first.Driver, first.Input.WithAssemblyName("Second.App"));
        AssertRegistration(second, "Second.App", "SecondApp", empty: false);
        second.Generated.ShouldContain("Getter = dto => ((t0)dto).Name");
        second.Generated.ShouldNotContain("namespace First.App;");
        second.Generated.ShouldNotContain("AddFromFirstApp");
    }

    [Fact]
    public void overridden_property_is_emitted_once_and_registration_succeeds()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public abstract class BaseRequest
            {
                public virtual string Name { get; set; } = "";
            }

            public class MyRequest : BaseRequest
            {
                public override string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        Regex.Count(result.Generated, @"GetProperty\(nameof\(t0\.Name\),").ShouldBe(1);

        var (assembly, cache) = LoadAndRegister(result);
        cache[assembly.GetType("MyApp.MyRequest")!].Properties!.Count.ShouldBe(1);
    }

    [Fact]
    public void attributes_of_overridden_properties_are_inherited()
    {
        const string source =
            """
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public abstract class BaseRequest
            {
                [JsonIgnore]
                public virtual string Secret { get; set; } = "";

                [DontInject]
                public virtual string Internal { get; set; } = "";
            }

            public class MyRequest : BaseRequest
            {
                public override string Secret { get; set; } = "";
                public override string Internal { get; set; } = "";
                public string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("nameof(t0.Name)");
        result.Generated.ShouldNotContain("Secret");
        result.Generated.ShouldNotContain("nameof(t0.Internal)");
    }

    [Fact]
    public void single_accessor_property_overrides_match_runtime_and_required_members_still_initialize()
    {
        const string source =
            """
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class BaseRequest
            {
                public virtual string GetOnlyOverride { get; set; } = "";
                public virtual string SetOnlyOverride { get; set; } = "";
                public virtual required string RequiredSetOverride { get; set; }
                public virtual string InitGetOverride { get; init; } = "";
                public string HiddenReadOnly { get; set; } = "";
            }

            public class DerivedRequest : BaseRequest
            {
                public override string GetOnlyOverride { get => base.GetOnlyOverride; }
                public override string SetOnlyOverride { set => base.SetOnlyOverride = value; }
                public override required string RequiredSetOverride { set => base.RequiredSetOverride = value; }
                public override string InitGetOverride { get => base.InitGetOverride; }
                public new string HiddenReadOnly { get; } = "";
                public required string PrivateGetRequired { private get; set; }
                [JsonIgnore]
                public required string JsonIgnoredRequired { get; set; }
                [DontInject]
                public required string DontInjectRequired { get; set; }
                public required string RequiredField;
                public string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<DerivedRequest>
            {
                public override Task HandleAsync(DerivedRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("RequiredSetOverride = default!");
        result.Generated.ShouldContain("PrivateGetRequired = default!");
        result.Generated.ShouldContain("JsonIgnoredRequired = default!");
        result.Generated.ShouldContain("DontInjectRequired = default!");
        result.Generated.ShouldContain("RequiredField = default!");
        result.Generated.ShouldContain("GetProperty(nameof(t0.Name),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.GetOnlyOverride),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.SetOnlyOverride),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.RequiredSetOverride),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.InitGetOverride),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.HiddenReadOnly),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.PrivateGetRequired),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.JsonIgnoredRequired),");
        result.Generated.ShouldNotContain("GetProperty(nameof(t0.DontInjectRequired),");

        var (assembly, cache) = LoadAndRegister(result);

        var reqType = assembly.GetType("MyApp.DerivedRequest")!;
        var entry = cache[reqType];
        var props = entry.Properties!;
        props.Count.ShouldBe(1);
        props.ContainsKey(reqType.GetProperty("Name")!).ShouldBeTrue();

        var instance = entry.ObjectFactory!();
        instance.ShouldBeOfType(reqType);
    }

    [Fact]
    public void hidden_properties_with_a_different_type_register_the_most_derived_declaration()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class BaseAddress { public string Id { get; set; } = ""; }
            public class Address : BaseAddress { public new int Id { get; set; } }

            public class GenericBase<T> { public T Code { get; set; } = default!; public T Same { get; set; } = default!; }
            public class Item : GenericBase<string> { public new int Code { get; set; } public new string Same { get; set; } = ""; }

            public class WideBase { public object Value { get; set; } = default!; public long Number { get; set; } }
            public class Wide : WideBase { public new string? Value { get; set; } public new int Number { get; set; } }

            public struct Point { public int X { get; set; } }

            public class UpdateRequest
            {
                public string? Name { get; set; }
                public (int A, string? B) Pair { get; set; }
                public Point Location { get; set; }
                public Point? MaybeLocation { get; set; }
                public int? Count { get; set; }
                public System.Collections.Generic.List<string?> Tags { get; set; } = [];
                public System.Collections.Generic.List<string?>? MoreTags { get; set; }
                public Address Address { get; set; } = new();
                public Address? OtherAddress { get; set; }
                public Item Item { get; set; } = new();
                public Wide Wide { get; set; } = new();
            }

            public class MyEndpoint : Endpoint<UpdateRequest>
            {
                public override Task HandleAsync(UpdateRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Output.GetDiagnostics()
              .Where(static d => d.Severity == DiagnosticSeverity.Warning && d.Location.SourceTree?.FilePath.EndsWith("ReflectionData.g.cs") is true)
              .ShouldBeEmpty();
        result.Generated.ShouldNotContain("typeof(string?)");

        //property types are aliased once and dto aliases are reused
        Regex.Count(result.Generated, @"global::System\.Collections\.Generic\.List<string\?>").ShouldBe(1);
        Regex.Count(result.Generated, @"global::MyApp\.Address\b(?!Base)").ShouldBe(2); // dto alias + its comment
        result.Generated.ShouldContain("using p0 = global::MyApp.Point?;");
        result.Generated.ShouldContain("typeof(string))!");

        var (assembly, cache) = LoadAndRegister(result);

        AssertMostDerived("MyApp.Address", "Id", 5);
        AssertMostDerived("MyApp.Item", "Code", 7);
        AssertMostDerived("MyApp.Item", "Same", "same");
        AssertMostDerived("MyApp.Wide", "Value", "value");
        AssertMostDerived("MyApp.Wide", "Number", 9);
        AssertMostDerived("MyApp.Point", "X", 3);
        AssertMostDerived("MyApp.UpdateRequest", "Name", "name");

        void AssertMostDerived(string typeName, string propName, object value)
        {
            var type = assembly.GetType(typeName)!;
            var def = MostDerivedProperty(cache, type, propName);
            var instance = Activator.CreateInstance(type)!;
            def.Setter!(instance, value);
            def.Getter!(instance).ShouldBe(value);
        }
    }

    [Fact]
    public void hidden_endpoint_service_property_with_a_different_type_registers_the_most_derived_declaration()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public abstract class MyBaseEndpoint : EndpointWithoutRequest<string>
            {
                public object Service { get; set; } = default!;
            }

            public class MyEndpoint : MyBaseEndpoint
            {
                [KeyedService("A")]
                public new string Service { get; set; } = default!;

                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);

        var (assembly, cache) = LoadAndRegister(result);

        var epType = assembly.GetType("MyApp.MyEndpoint")!;
        MostDerivedProperty(cache, epType, "Service").ServiceKey.ShouldBe("A");
    }

    [Fact]
    public void sets_required_members_constructor_omits_default_initializer_overrides()
    {
        const string source =
            """
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                [SetsRequiredMembers]
                public MyRequest()
                {
                    Name = "from-ctor";
                }

                public required string Name { get; set; }
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldNotContain("Name = default!");

        var (assembly, cache) = LoadAndRegister(result);

        var reqType = assembly.GetType("MyApp.MyRequest")!;
        var entry = cache[reqType];
        var instance = entry.ObjectFactory!();
        reqType.GetProperty("Name")!.GetValue(instance).ShouldBe("from-ctor");
    }

    [Fact]
    public void derived_request_still_initializes_required_members_when_only_base_ctor_sets_them()
    {
        const string source =
            """
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class BaseRequest
            {
                [SetsRequiredMembers]
                public BaseRequest()
                {
                    Name = "from-base";
                }

                public BaseRequest(string extra) { }

                public required string Name { get; set; }
            }

            public class DerivedRequest : BaseRequest
            {
                public DerivedRequest(string extra) : base(extra)
                {
                    Extra = extra;
                }

                public string Extra { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<DerivedRequest>
            {
                public override Task HandleAsync(DerivedRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("Name = default!");

        var (assembly, cache) = LoadAndRegister(result);

        var reqType = assembly.GetType("MyApp.DerivedRequest")!;
        var entry = cache[reqType];
        var instance = entry.ObjectFactory!();
        instance.ShouldBeOfType(reqType);
    }

    [Fact]
    public void reused_driver_regenerates_when_required_members_change()
    {
        static string Source(bool requiredField, bool setsRequiredMembers)
            => $$"""
                 using System.Diagnostics.CodeAnalysis;
                 using System.Threading;
                 using System.Threading.Tasks;
                 using FastEndpoints;

                 namespace MyApp;

                 public class MyRequest
                 {
                     {{(setsRequiredMembers ? "[SetsRequiredMembers]" : "")}}
                     public MyRequest()
                     {
                         Name = "from-ctor";
                         Tag = "from-ctor";
                     }

                     public required string Name { get; set; }
                     public {{(requiredField ? "required " : "")}}string Tag;
                 }

                 public class MyEndpoint : Endpoint<MyRequest>
                 {
                     public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
                 }
                 """;

        var first = Run(Source(requiredField: false, setsRequiredMembers: false));
        AssertCompiles(first);
        first.Generated.ShouldContain("Name = default!");
        first.Generated.ShouldNotContain("Tag = default!");

        var tree1 = first.Input.SyntaxTrees.Single();
        var comp2 = first.Input.ReplaceSyntaxTree(
            tree1,
            CSharpSyntaxTree.ParseText(Source(requiredField: true, setsRequiredMembers: false), (CSharpParseOptions)tree1.Options));
        var second = Execute(first.Driver, comp2);
        AssertCompiles(second);
        second.Generated.ShouldContain("Name = default!");
        second.Generated.ShouldContain("Tag = default!");

        var tree2 = second.Input.SyntaxTrees.Single();
        var comp3 = second.Input.ReplaceSyntaxTree(
            tree2,
            CSharpSyntaxTree.ParseText(Source(requiredField: true, setsRequiredMembers: true), (CSharpParseOptions)tree2.Options));
        var third = Execute(second.Driver, comp3);
        AssertCompiles(third);
        third.Generated.ShouldNotContain("Name = default!");
        third.Generated.ShouldNotContain("Tag = default!");
    }

    [Fact]
    public void conditional_json_ignore_property_is_bindable()
    {
        const string source =
            """
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                public string? Conditional { get; set; }

                [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
                public string? Always { get; set; }

                [JsonIgnore]
                public string? Plain { get; set; }

                public string Name { get; set; } = "";
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("nameof(t0.Conditional)");
        result.Generated.ShouldNotContain("nameof(t0.Always)");
        result.Generated.ShouldNotContain("nameof(t0.Plain)");
    }

    [Fact]
    public void keyword_property_names_are_escaped()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                public string @class { get; set; } = "";
                public required string @event { get; set; }
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("nameof(t0.@class)");
        result.Generated.ShouldContain("{ @event = default! }");
    }

    [Fact]
    public void object_factory_uses_constructor_with_fewest_parameters()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class Overloaded
            {
                public Overloaded(string name) { }
                public Overloaded(int id) { }
                public string Name { get; set; } = "";
            }

            public class Parameterless
            {
                public Parameterless() { }
                public Parameterless(string a, string b) { }
                public string Name { get; set; } = "";
            }

            public class NonPublic
            {
                internal NonPublic(string name) { }
                public string Name { get; set; } = "";
            }

            public class Optional
            {
                public Optional(int x = 0, int y = 0) { }
                public Optional(string name) { }
                public string Name { get; set; } = "";
            }

            public class Params
            {
                public Params(string? filter = null, params string[] tags) { }
                public string Name { get; set; } = "";
            }

            public class MyRequest
            {
                public Overloaded A { get; set; } = null!;
                public Parameterless B { get; set; } = null!;
                public NonPublic C { get; set; } = null!;
                public Optional D { get; set; } = null!;
                public Params E { get; set; } = null!;
            }

            public class MyEndpoint : Endpoint<MyRequest>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("ObjectFactory = () => new t1(default(string)!)");
        result.Generated.ShouldContain("ObjectFactory = () => new t2()");
        result.Generated.ShouldContain("using t3 = global::MyApp.NonPublic;");
        result.Generated.ShouldNotContain("new t3(");
        result.Generated.ShouldContain("ObjectFactory = () => new t4(default(string)!)");
        result.Generated.ShouldContain("ObjectFactory = () => new t5()");
    }

    [Fact]
    public void request_dto_is_resolved_from_the_endpoint_base_type()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class MyRequest
            {
                public string Name { get; set; } = "";
            }

            public class MyResponse
            {
                public string Result { get; set; } = "";
            }

            public abstract class MyBase<TResponse> : Endpoint<MyRequest, TResponse> where TResponse : notnull;

            public class CustomBaseEndpoint : MyBase<MyResponse>
            {
                public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
            }

            public class NoRequestEndpoint : EndpointWithoutRequest<MyResponse>
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldContain("using t0 = global::MyApp.MyRequest;");
        result.Generated.ShouldNotContain("MyResponse");
    }

    [Fact]
    public void emitted_type_names_are_not_shadowed_by_root_namespace_members()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp.System
            {
                public class Marker;
            }

            namespace TestApp
            {
                public class ReflectionCache;
                public class Unsafe;
                public class CultureInfo;
            }

            namespace MyApp
            {
                public struct Point
                {
                    public int X { get; set; }
                }

                public class MyRequest
                {
                    public global::System.Guid Id { get; set; }
                    public Point Location { get; set; }
                }

                public class MyEndpoint : Endpoint<MyRequest>
                {
                    public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
                }
            }
            """;

        var result = Run(source);

        AssertCompiles(result);
        result.Generated.ShouldMatch(@"using [tp]\d+ = global::System\.Guid;");
    }

    [Fact]
    public void reused_driver_regenerates_after_nested_dto_property_rename()
    {
        static string Source(string propertyName)
            => $$"""
                 using System.Threading;
                 using System.Threading.Tasks;
                 using FastEndpoints;

                 namespace MyApp;

                 public class Address
                 {
                     public string {{propertyName}} { get; set; } = "";
                 }

                 public class MyRequest
                 {
                     public Address Address { get; set; } = new();
                 }

                 public class MyEndpoint : Endpoint<MyRequest>
                 {
                     public override Task HandleAsync(MyRequest req, CancellationToken ct) => Task.CompletedTask;
                 }
                 """;

        var first = Run(Source("Street"));
        AssertCompiles(first);

        //a same-length rename and an unrelated edit must both be reflected by the cached driver
        var tree = first.Input.SyntaxTrees.Single();
        var renamed = first.Input.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(Source("Avenue"), (CSharpParseOptions)tree.Options));
        var second = Execute(first.Driver, renamed.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace MyApp; class Unrelated;", (CSharpParseOptions)tree.Options)));

        AssertCompiles(second);
        second.Generated.ShouldContain("nameof(t1.Avenue)");
        second.Generated.ShouldNotContain("Street");
    }

    [Fact]
    public void dont_register_attribute_omits_endpoint_from_reflection_data()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace MyApp;

            public class SkippedRequest
            {
                public string Name { get; set; } = "";
            }

            [DontRegister]
            public class SkippedEndpoint : Endpoint<SkippedRequest, string>
            {
                public override void Configure() { }

                public override Task HandleAsync(SkippedRequest req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = Run(source);

        AssertRegistration(result, "TestApp", "TestApp", empty: true);
        result.Generated.ShouldNotContain("SkippedRequest");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static readonly ImmutableArray<MetadataReference> _references = GeneratorTestHarness.GetReferences(
        typeof(Endpoint<>).Assembly,
        typeof(HideFromDocsAttribute).Assembly,
        typeof(Microsoft.AspNetCore.Http.IResult).Assembly,
        typeof(Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult).Assembly);

    static void AssertCompiles(GeneratorTestResult result)
        => GeneratorTestHarness.AssertCompiles(result);

    static string RunGenerator(string source, out ImmutableArray<Diagnostic> generatorDiagnostics)
        => RunGenerator(source, out generatorDiagnostics, out _);

    static string RunGenerator(
        string source,
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        out Compilation outputCompilation)
    {
        var result = Run(source);
        generatorDiagnostics = result.Diagnostics;
        outputCompilation = result.Output;

        return result.Generated;
    }

    static GeneratorTestResult Run(string source, string? assemblyName = "TestApp")
        => GeneratorTestHarness.Run<ReflectionGenerator>([source], _references, assemblyName, hintFileName: "ReflectionData.g.cs");

    //emits and loads the generated assembly, then registers its reflection data with a new cache. requires the default "TestApp" assembly name.
    static (Assembly Assembly, ReflectionCache Cache) LoadAndRegister(GeneratorTestResult result)
    {
        using var stream = new MemoryStream();
        result.Output.Emit(stream).Success.ShouldBeTrue();
        var assembly = Assembly.Load(stream.ToArray());
        var cache = new ReflectionCache();
        assembly.GetType("TestApp.GeneratedReflection")!.GetMethod("AddFromTestApp")!.Invoke(null, [cache]);

        return (assembly, cache);
    }

    //asserts a single registered property with the given name and returns the definition of its most derived declaration
    static PropertyDefinition MostDerivedProperty(ReflectionCache cache, Type type, string propName)
    {
        var prop = type.GetProperties().Single(p => p.Name == propName && p.DeclaringType == type);
        var props = cache[type].Properties!;
        props.Keys.Count(p => p.Name == propName).ShouldBe(1);

        return props[prop];
    }

    static GeneratorTestResult Execute(GeneratorDriver driver, Compilation compilation)
        => GeneratorTestHarness.Step(driver, compilation, "ReflectionData.g.cs");

    static void AssertRegistration(GeneratorTestResult result, string expectedNamespace, string expectedMethod, bool empty)
    {
        result.Input.GetDiagnostics()
              .Where(static d => d.Severity == DiagnosticSeverity.Error)
              .ShouldBeEmpty();
        result.GeneratedTreeCount.ShouldBe(1);
        AssertCompiles(result);
        result.Generated.ShouldContain($"namespace {expectedNamespace};");
        result.Generated.ShouldContain($"public static global::FastEndpoints.ReflectionCache AddFrom{expectedMethod}(this global::FastEndpoints.ReflectionCache cache)");
        result.Generated.ShouldContain("return cache;");

        if (empty)
            result.Generated.ShouldNotContain("cache.TryAdd");
    }
}
