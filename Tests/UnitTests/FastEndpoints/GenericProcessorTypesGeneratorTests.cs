using System.Collections.Immutable;
using FastEndpoints;
using FastEndpoints.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Generator;

public class GenericProcessorTypesGeneratorTests
{
    const string ProcessorsSource =
        """
        using System.Threading;
        using System.Threading.Tasks;
        using FastEndpoints;

        namespace TestApp;

        public class MyPre<TRequest> : IPreProcessor<TRequest>
        {
            public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
        }

        public class MyPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
        {
            public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
        }

        """;

    [Fact]
    public void processors_are_closed_with_the_dto_types_of_each_endpoint()
    {
        const string source =
            ProcessorsSource +
            """
            public class ReqA;
            public class ResA;
            public class ReqB;
            public class ResB;

            public class EndpointA : Endpoint<ReqA, ResA>
            {
                public override Task HandleAsync(ReqA req, CancellationToken ct) => Task.CompletedTask;
            }

            public class EndpointB : Ep.Req<ReqB>.Res<ResB>
            {
                public override Task HandleAsync(ReqB req, CancellationToken ct) => Task.CompletedTask;
            }

            public class NoRequestEndpoint : EndpointWithoutRequest<ResA>
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }

            public class OpenGenericEndpoint<T> : Endpoint<T> where T : notnull
            {
                public override Task HandleAsync(T req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        generated.ShouldContain("Preserve<global::TestApp.MyPre<global::TestApp.ReqA>>()");
        generated.ShouldContain("Preserve<global::TestApp.MyPre<global::TestApp.ReqB>>()");
        generated.ShouldContain("Preserve<global::TestApp.MyPre<global::FastEndpoints.EmptyRequest>>()");
        generated.ShouldContain("Preserve<global::TestApp.MyPost<global::TestApp.ReqA, global::TestApp.ResA>>()");
        generated.ShouldContain("Preserve<global::TestApp.MyPost<global::TestApp.ReqB, global::TestApp.ResB>>()");
        generated.ShouldContain("Preserve<global::TestApp.MyPost<global::FastEndpoints.EmptyRequest, global::TestApp.ResA>>()");
        generated.ShouldNotContain("Preserve<global::TestApp.MyPost<global::TestApp.ReqA, global::TestApp.ResB>>()");
        generated.ShouldNotContain("<T>");
    }

    [Fact]
    public void processors_are_only_closed_with_dtos_satisfying_their_constraints()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public interface IHasTenant;

            public class TenantPre<TRequest> : IPreProcessor<TRequest> where TRequest : IHasTenant
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class TenantPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse> where TRequest : class, IHasTenant where TResponse : new()
            {
                public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class BaseTenantRequest : IHasTenant;
            public class TenantRequest : BaseTenantRequest;
            public class OtherRequest;
            public class Response;
            public class NoDefaultCtorResponse(string name);

            public class TenantEndpoint : Endpoint<TenantRequest, Response>
            {
                public override Task HandleAsync(TenantRequest req, CancellationToken ct) => Task.CompletedTask;
            }

            public class NoDefaultCtorEndpoint : Endpoint<BaseTenantRequest, NoDefaultCtorResponse>
            {
                public override Task HandleAsync(BaseTenantRequest req, CancellationToken ct) => Task.CompletedTask;
            }

            public class OtherEndpoint : Endpoint<OtherRequest, Response>
            {
                public override Task HandleAsync(OtherRequest req, CancellationToken ct) => Task.CompletedTask;
            }

            public class HealthEndpoint : EndpointWithoutRequest
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.TenantPre<global::TestApp.TenantRequest>>()");
        generated.ShouldContain("Preserve<global::TestApp.TenantPre<global::TestApp.BaseTenantRequest>>()");
        generated.ShouldContain("Preserve<global::TestApp.TenantPost<global::TestApp.TenantRequest, global::TestApp.Response>>()");
        generated.ShouldNotContain("OtherRequest");
        generated.ShouldNotContain("EmptyRequest");
        generated.ShouldNotContain("NoDefaultCtorResponse");
    }

    [Fact]
    public void constraints_referencing_type_parameters_are_closed_with_the_endpoint_dtos()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public interface IKeyed<T>;
            public interface IResultOf<TRequest>;

            public class KeyedPre<TRequest> : IPreProcessor<TRequest> where TRequest : IKeyed<TRequest>
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class ResultPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse> where TResponse : IResultOf<TRequest>
            {
                public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class ReqA : IKeyed<ReqA>;
            public class ResA : IResultOf<ReqA>;
            public class ReqB : IKeyed<ReqA>;
            public class ResB : IResultOf<ReqA>;

            public class EndpointA : Endpoint<ReqA, ResA>
            {
                public override Task HandleAsync(ReqA req, CancellationToken ct) => Task.CompletedTask;
            }

            public class EndpointB : Endpoint<ReqB, ResB>
            {
                public override Task HandleAsync(ReqB req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.ShouldContain("Preserve<global::TestApp.KeyedPre<global::TestApp.ReqA>>()");
        generated.ShouldContain("Preserve<global::TestApp.ResultPost<global::TestApp.ReqA, global::TestApp.ResA>>()");
        generated.ShouldNotContain("ReqB");
    }

    [Fact]
    public void type_list_is_generated_without_open_generic_processors()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class MyEndpoint : EndpointWithoutRequest
            {
                public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
            }

            public static class Usage
            {
                public static global::System.Collections.Generic.List<global::System.Type> Types => GenericProcessorTypes.All;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.ShouldContain("public static class GenericProcessorTypes");
        generated.ShouldNotContain("Preserve<global::");
    }

    [Fact]
    public void reused_driver_tracks_endpoint_request_type_changes()
    {
        static string Source(string requestType)
            => ProcessorsSource +
               $$"""
                 public class ReqA;
                 public class ReqB;

                 public class MyEndpoint : Endpoint<{{requestType}}>
                 {
                     public override Task HandleAsync({{requestType}} req, CancellationToken ct) => Task.CompletedTask;
                 }
                 """;

        var first = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([Source("ReqA")], _references);
        var second = GeneratorTestHarness.Step(
            first.Driver,
            first.Input.ReplaceSyntaxTree(
                first.Input.SyntaxTrees.Single(),
                CSharpSyntaxTree.ParseText(Source("ReqB"), GeneratorTestHarness.DefaultParseOptions)));

        GeneratorTestHarness.AssertCompiles(second);
        second.Generated.ShouldContain("Preserve<global::TestApp.MyPre<global::TestApp.ReqB>>()");
        second.Generated.ShouldNotContain("ReqA");
    }

    [Fact]
    public void reused_driver_tracks_adding_and_removing_the_only_processors()
    {
        const string endpointSource =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class Req;

            public class MyEndpoint : Endpoint<Req>
            {
                public override Task HandleAsync(Req req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var first = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([endpointSource], _references);
        first.Generated.ShouldNotContain("Preserve<global::");

        var processorsTree = CSharpSyntaxTree.ParseText(ProcessorsSource, GeneratorTestHarness.DefaultParseOptions);
        var second = GeneratorTestHarness.Step(first.Driver, first.Input.AddSyntaxTrees(processorsTree));

        GeneratorTestHarness.AssertCompiles(second);
        second.Generated.ShouldContain("Preserve<global::TestApp.MyPre<global::TestApp.Req>>()");
        second.Generated.ShouldContain("Preserve<global::TestApp.MyPost<global::TestApp.Req, object>>()");

        var third = GeneratorTestHarness.Step(second.Driver, second.Input.RemoveSyntaxTrees(processorsTree));

        GeneratorTestHarness.AssertCompiles(third);
        third.Generated.ShouldNotContain("Preserve<global::");
    }

    [Fact]
    public void new_constraint_skips_dtos_with_required_members_unless_ctor_sets_them()
    {
        const string source =
            """
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class NewPre<TRequest> : IPreProcessor<TRequest> where TRequest : new()
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class NewPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
                where TRequest : new()
                where TResponse : new()
            {
                public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class StructPre<TRequest> : IPreProcessor<TRequest> where TRequest : struct
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class RequiredPropReq { public required string Name { get; set; } }
            public class BaseWithRequired { public required string BaseName { get; set; } }
            public class DerivedRequiredReq : BaseWithRequired { }
            public class RequiredFieldReq { public required string Field; }
            public struct RequiredStructReq { public required string Name { get; set; } }
            public class SetsRequiredReq
            {
                public required string Name { get; set; }

                [SetsRequiredMembers]
                public SetsRequiredReq() => Name = "";
            }
            public struct SetsRequiredStructReq
            {
                public required string Name { get; set; }

                [SetsRequiredMembers]
                public SetsRequiredStructReq() => Name = "";
            }
            public struct PlainStructReq { public int Id { get; set; } }
            public enum PlainEnumReq { A, B }
            public class RequiredRes { public required string Value { get; set; } }
            public class ValidRes { }

            public class EpRequiredProp : Endpoint<RequiredPropReq, ValidRes>
            {
                public override Task HandleAsync(RequiredPropReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpDerivedRequired : Endpoint<DerivedRequiredReq, ValidRes>
            {
                public override Task HandleAsync(DerivedRequiredReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpRequiredField : Endpoint<RequiredFieldReq, ValidRes>
            {
                public override Task HandleAsync(RequiredFieldReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpRequiredStruct : Endpoint<RequiredStructReq, ValidRes>
            {
                public override Task HandleAsync(RequiredStructReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpSetsRequired : Endpoint<SetsRequiredReq, ValidRes>
            {
                public override Task HandleAsync(SetsRequiredReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpSetsRequiredStruct : Endpoint<SetsRequiredStructReq, ValidRes>
            {
                public override Task HandleAsync(SetsRequiredStructReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpPlainStruct : Endpoint<PlainStructReq, RequiredRes>
            {
                public override Task HandleAsync(PlainStructReq req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpPlainEnum : Endpoint<PlainEnumReq, ValidRes>
            {
                public override Task HandleAsync(PlainEnumReq req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        generated.ShouldNotContain("NewPre<global::TestApp.RequiredPropReq>");
        generated.ShouldNotContain("NewPre<global::TestApp.DerivedRequiredReq>");
        generated.ShouldNotContain("NewPre<global::TestApp.RequiredFieldReq>");
        generated.ShouldNotContain("NewPre<global::TestApp.RequiredStructReq>");
        generated.ShouldNotContain("NewPost<global::TestApp.RequiredPropReq,");
        generated.ShouldNotContain("NewPost<global::TestApp.DerivedRequiredReq,");
        generated.ShouldNotContain("NewPost<global::TestApp.RequiredFieldReq,");
        generated.ShouldNotContain("NewPost<global::TestApp.RequiredStructReq,");
        generated.ShouldNotContain("NewPost<global::TestApp.PlainStructReq, global::TestApp.RequiredRes>");

        generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.SetsRequiredReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPost<global::TestApp.SetsRequiredReq, global::TestApp.ValidRes>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.SetsRequiredStructReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPost<global::TestApp.SetsRequiredStructReq, global::TestApp.ValidRes>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.PlainStructReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.PlainEnumReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.NewPost<global::TestApp.PlainEnumReq, global::TestApp.ValidRes>>()");

        generated.ShouldContain("Preserve<global::TestApp.StructPre<global::TestApp.RequiredStructReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.StructPre<global::TestApp.SetsRequiredStructReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.StructPre<global::TestApp.PlainStructReq>>()");
        generated.ShouldContain("Preserve<global::TestApp.StructPre<global::TestApp.PlainEnumReq>>()");
    }

    [Fact]
    public void unmanaged_constraint_only_closes_with_unmanaged_value_types()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public class UnmanagedPre<TRequest> : IPreProcessor<TRequest> where TRequest : unmanaged
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class UnmanagedPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
                where TRequest : unmanaged
                where TResponse : unmanaged
            {
                public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public struct UnmanagedStruct { public int Id; }
            public struct ManagedStruct { public string Name { get; set; } }
            public enum MyEnum { First, Second }
            public class ClassReq { public int Id { get; set; } }

            public class EpUnmanaged : Endpoint<UnmanagedStruct, MyEnum>
            {
                public override Task HandleAsync(UnmanagedStruct req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpManagedStruct : Endpoint<ManagedStruct, UnmanagedStruct>
            {
                public override Task HandleAsync(ManagedStruct req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpManagedResponse : Endpoint<UnmanagedStruct, ManagedStruct>
            {
                public override Task HandleAsync(UnmanagedStruct req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpNullableResponse : Endpoint<UnmanagedStruct, int?>
            {
                public override Task HandleAsync(UnmanagedStruct req, CancellationToken ct) => Task.CompletedTask;
            }
            public class EpClass : Endpoint<ClassReq, UnmanagedStruct>
            {
                public override Task HandleAsync(ClassReq req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generated, outputCompilation, generatorDiagnostics) = Run(source);

        generatorDiagnostics.ShouldBeEmpty();
        outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        generated.ShouldContain("Preserve<global::TestApp.UnmanagedPre<global::TestApp.UnmanagedStruct>>()");
        generated.ShouldContain("Preserve<global::TestApp.UnmanagedPost<global::TestApp.UnmanagedStruct, global::TestApp.MyEnum>>()");
        generated.ShouldNotContain("global::TestApp.ManagedStruct");
        generated.ShouldNotContain("ClassReq");
        generated.ShouldNotContain("int?");
    }

    [Fact]
    public void reused_driver_tracks_required_member_changes()
    {
        static string Source(string modifier)
            => $$"""
                 using System.Threading;
                 using System.Threading.Tasks;
                 using FastEndpoints;

                 namespace TestApp;

                 public class NewPre<TRequest> : IPreProcessor<TRequest> where TRequest : new()
                 {
                     public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
                 }

                 public class Req { public {{modifier}}string Name { get; set; } = ""; }

                 public class MyEndpoint : Endpoint<Req>
                 {
                     public override Task HandleAsync(Req req, CancellationToken ct) => Task.CompletedTask;
                 }
                 """;

        var initial = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([Source("")], _references);
        GeneratorTestHarness.AssertCompiles(initial);
        initial.Generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.Req>>()");

        var treeWithRequired = CSharpSyntaxTree.ParseText(Source("required "), GeneratorTestHarness.DefaultParseOptions);
        var withRequired = GeneratorTestHarness.Step(
            initial.Driver,
            initial.Input.ReplaceSyntaxTree(initial.Input.SyntaxTrees.Single(), treeWithRequired));

        GeneratorTestHarness.AssertCompiles(withRequired);
        withRequired.GeneratedTreeCount.ShouldBe(1);
        withRequired.Generated.ShouldNotContain("NewPre<global::TestApp.Req>");

        var withoutRequired = GeneratorTestHarness.Step(
            withRequired.Driver,
            withRequired.Input.ReplaceSyntaxTree(
                treeWithRequired,
                CSharpSyntaxTree.ParseText(Source(""), GeneratorTestHarness.DefaultParseOptions)));

        GeneratorTestHarness.AssertCompiles(withoutRequired);
        withoutRequired.Generated.ShouldContain("Preserve<global::TestApp.NewPre<global::TestApp.Req>>()");
    }

    [Fact]
    public void constraint_types_are_checked_for_nested_endpoints_and_keyword_namespaces()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp.@event;

            public interface IKeyed;

            public class KeyedPre<TRequest> : IPreProcessor<TRequest> where TRequest : IKeyed
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class Req : IKeyed;
            public class NestedReq : IKeyed;

            public class MyEndpoint : Endpoint<Req>
            {
                public override Task HandleAsync(Req req, CancellationToken ct) => Task.CompletedTask;
            }

            public static class Outer
            {
                public class NestedEndpoint : Endpoint<NestedReq>
                {
                    public override Task HandleAsync(NestedReq req, CancellationToken ct) => Task.CompletedTask;
                }
            }
            """;

        var result = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([source], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("Preserve<global::TestApp.@event.KeyedPre<global::TestApp.@event.Req>>()");
        result.Generated.ShouldContain("Preserve<global::TestApp.@event.KeyedPre<global::TestApp.@event.NestedReq>>()");
    }

    [Fact]
    public void reused_driver_tracks_dto_interface_changes_for_constraint_types()
    {
        //only the dto changes, so the endpoint's transform result stays equal
        static string Source(string baseList)
            => $$"""
                 using System.Threading;
                 using System.Threading.Tasks;
                 using FastEndpoints;

                 namespace TestApp;

                 public interface IKeyed;

                 public class KeyedPre<TRequest> : IPreProcessor<TRequest> where TRequest : IKeyed
                 {
                     public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
                 }

                 public class Req {{baseList}};

                 public class MyEndpoint : Endpoint<Req>
                 {
                     public override Task HandleAsync(Req req, CancellationToken ct) => Task.CompletedTask;
                 }
                 """;

        var initial = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([Source(": IKeyed")], _references);
        GeneratorTestHarness.AssertCompiles(initial);
        initial.Generated.ShouldContain("Preserve<global::TestApp.KeyedPre<global::TestApp.Req>>()");

        var treeWithoutInterface = CSharpSyntaxTree.ParseText(Source(""), GeneratorTestHarness.DefaultParseOptions);
        var withoutInterface = GeneratorTestHarness.Step(
            initial.Driver,
            initial.Input.ReplaceSyntaxTree(initial.Input.SyntaxTrees.Single(), treeWithoutInterface));

        GeneratorTestHarness.AssertCompiles(withoutInterface);
        withoutInterface.Generated.ShouldNotContain("KeyedPre<global::TestApp.Req>");

        var withInterface = GeneratorTestHarness.Step(
            withoutInterface.Driver,
            withoutInterface.Input.ReplaceSyntaxTree(
                treeWithoutInterface,
                CSharpSyntaxTree.ParseText(Source(": IKeyed"), GeneratorTestHarness.DefaultParseOptions)));

        GeneratorTestHarness.AssertCompiles(withInterface);
        withInterface.Generated.ShouldContain("Preserve<global::TestApp.KeyedPre<global::TestApp.Req>>()");
    }

    //members of the root namespace that would shadow unqualified names in the generated code
    const string ShadowingSource =
        """
        namespace TestApp.System { public class Marker; }
        namespace TestApp { public class Type; public class List<T>; }
        """;

    [Fact]
    public void dont_register_attribute_omits_endpoints_and_processors()
    {
        const string source =
            ProcessorsSource +
            """
            public class IncludedReq;
            public class SkippedReq;

            [DontRegister]
            public class SkippedPre<TRequest> : IPreProcessor<TRequest>
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class IncludedEndpoint : Endpoint<IncludedReq>
            {
                public override Task HandleAsync(IncludedReq req, CancellationToken ct) => Task.CompletedTask;
            }

            [DontRegister]
            public class SkippedEndpoint : Endpoint<SkippedReq>
            {
                public override Task HandleAsync(SkippedReq req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([source, ShadowingSource], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("Preserve<global::TestApp.MyPre<global::TestApp.IncludedReq>>()");
        result.Generated.ShouldNotContain("SkippedPre");
        result.Generated.ShouldNotContain("SkippedReq");
    }

    [Fact]
    public void obsolete_types_do_not_raise_obsolete_warnings_in_generated_code()
    {
        const string source =
            ProcessorsSource +
            """
            [global::System.Obsolete]
            public class OldReq;

            [global::System.Obsolete("use something else")]
            public class OldPre<TRequest> : IPreProcessor<TRequest>
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class OldEndpoint : Endpoint<OldReq>
            {
                public override Task HandleAsync(OldReq req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([source, ShadowingSource], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("Preserve<global::TestApp.OldPre<global::TestApp.OldReq>>()");
        GeneratorTestHarness.AssertNoObsoleteWarningsInGeneratedCode(result);
    }


    [Fact]
    public void open_generic_record_processors_are_closed_with_endpoint_dtos()
    {
        const string source =
            """
            using System.Threading;
            using System.Threading.Tasks;
            using FastEndpoints;

            namespace TestApp;

            public record RecordPre<TRequest> : IPreProcessor<TRequest>
            {
                public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public record struct RecordStructPost<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
            {
                public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> ctx, CancellationToken ct) => Task.CompletedTask;
            }

            public class Req;
            public class Res;

            public class MyEndpoint : Endpoint<Req, Res>
            {
                public override Task HandleAsync(Req req, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([source, ShadowingSource], _references);

        GeneratorTestHarness.AssertCompiles(result);
        result.Generated.ShouldContain("Preserve<global::TestApp.RecordPre<global::TestApp.Req>>()");
        result.Generated.ShouldContain("Preserve<global::TestApp.RecordStructPost<global::TestApp.Req, global::TestApp.Res>>()");
    }

    static readonly ImmutableArray<MetadataReference> _references = GeneratorTestHarness.GetReferences(typeof(Endpoint<>).Assembly);

    //the compilation has no implicit usings, so the generated code must declare everything it uses
    static (string Generated, Compilation Output, IReadOnlyList<Diagnostic> Diagnostics) Run(string source)
    {
        var result = GeneratorTestHarness.Run<GenericProcessorTypesGenerator>([source, ShadowingSource], _references);
        result.GeneratedTreeCount.ShouldBe(1);

        return (result.Generated, result.Output, result.Diagnostics);
    }
}
