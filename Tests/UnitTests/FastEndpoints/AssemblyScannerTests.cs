using FastEndpoints;
using Xunit;

namespace AssemblyScanning;

public class AssemblyScannerTests
{
    [Fact]
    public void InterfaceTypes_MatchTypesImplementingThem()
    {
        var found = Scan([typeof(IMarkerA)]);

        found.ShouldContain(typeof(ImplementsA));
        found.ShouldContain(typeof(ImplementsBoth));
        found.ShouldNotContain(typeof(ImplementsB));
        found.ShouldNotContain(typeof(ImplementsNeither));
    }

    [Fact]
    public void MultipleInterfaceTypes_MatchAnyOfThemNotAllOfThem()
    {
        var found = Scan([typeof(IMarkerA), typeof(IMarkerB)]);

        found.ShouldContain(typeof(ImplementsA));   //only IMarkerA
        found.ShouldContain(typeof(ImplementsB));   //only IMarkerB
        found.ShouldContain(typeof(ImplementsBoth));
        found.ShouldNotContain(typeof(ImplementsNeither));
    }

    [Fact]
    public void NoInterfaceTypes_MatchesEveryTypeThatIsNotOtherwiseFiltered()
    {
        var found = Scan([], typeFilter: t => t.Namespace == typeof(AssemblyScannerTests).Namespace);

        found.ShouldContain(typeof(ImplementsA));
        found.ShouldContain(typeof(ImplementsNeither)); //no interface requirement to fail
    }

    [Fact]
    public void AbstractGenericAndInterfaceTypes_AreNeverReturned()
    {
        var found = Scan([typeof(IMarkerA)]);

        found.ShouldNotContain(typeof(IMarkerA));
        found.ShouldNotContain(typeof(AbstractImplementsA));
        found.ShouldNotContain(typeof(GenericImplementsA<>));
        found.ShouldNotContain(typeof(GenericImplementsA<int>));
    }

    [Fact]
    public void ExcludeAttribute_SkipsDecoratedTypes()
    {
        var found = Scan([typeof(IMarkerA)], excludeAttribute: typeof(ScannerExcludeAttribute));

        found.ShouldContain(typeof(ImplementsA));
        found.ShouldNotContain(typeof(ExcludedByAttribute));

        //without the exclude attribute configured, the very same type is discovered
        Scan([typeof(IMarkerA)]).ShouldContain(typeof(ExcludedByAttribute));
    }

    [Fact]
    public void TypeFilter_IsAppliedOnTopOfInterfaceMatching()
    {
        var found = Scan([typeof(IMarkerA)], typeFilter: t => t != typeof(ImplementsBoth));

        found.ShouldContain(typeof(ImplementsA));
        found.ShouldNotContain(typeof(ImplementsBoth));
    }

    [Fact]
    public void AutoDiscoveredAssembliesOnTheExclusionList_AreNotScanned()
    {
        //'FastEndpoints.Core' is prefix matched by the 'FastEndpoints' exclusion
        var tCoreAssembly = typeof(AssemblyScanOptions).Assembly;

        var found = AssemblyScanner.ScanForTypes(
                                       new()
                                       {
                                           AssemblyFilter = a => a == tCoreAssembly,
                                           TypeFilter = t => t == typeof(AssemblyScanOptions)
                                       })
                                   .ToArray();

        found.ShouldBeEmpty();
    }

    [Fact]
    public void ExplicitlyProvidedAssemblies_BypassTheExclusionList()
    {
        var tCoreAssembly = typeof(AssemblyScanOptions).Assembly;

        var found = AssemblyScanner.ScanForTypes(
                                       new()
                                       {
                                           Assemblies = [tCoreAssembly], //same excluded assembly as above, but requested by name
                                           AssemblyFilter = a => a == tCoreAssembly,
                                           TypeFilter = t => t == typeof(AssemblyScanOptions)
                                       })
                                   .ToArray();

        found.ShouldBe([typeof(AssemblyScanOptions)]);
    }

    static Type[] Scan(Type[] interfaceTypes, Func<Type, bool>? typeFilter = null, Type? excludeAttribute = null)
        => AssemblyScanner.ScanForTypes(
                              new()
                              {
                                  DisableAutoDiscovery = true,
                                  Assemblies = [typeof(AssemblyScannerTests).Assembly],
                                  InterfaceTypes = interfaceTypes,
                                  TypeFilter = typeFilter,
                                  ExcludeAttribute = excludeAttribute
                              })
                          .ToArray();
}

file interface IMarkerA;

file interface IMarkerB;

file sealed class ImplementsA : IMarkerA;

file sealed class ImplementsB : IMarkerB;

file sealed class ImplementsBoth : IMarkerA, IMarkerB;

file sealed class ImplementsNeither;

file abstract class AbstractImplementsA : IMarkerA;

file sealed class GenericImplementsA<T> : IMarkerA;

[AttributeUsage(AttributeTargets.Class)]
file sealed class ScannerExcludeAttribute : Attribute;

[ScannerExclude]
file sealed class ExcludedByAttribute : IMarkerA;
