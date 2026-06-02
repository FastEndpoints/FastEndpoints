using FastEndpoints;
using Xunit;

namespace Unit.FastEndpoints;

public class ServiceBoundEpPropsTests
{
    [Fact]
    public void Reflection_Path_Returns_Props_With_Correct_Service_Keys()
    {
        // ReflectionKeyedEp is a unique type not pre-registered in the static ReflectionCache,
        // so GetServiceBoundEpProps() falls back to reflection to read [KeyedService] attributes.
        var epDef = new EndpointDefinition(typeof(ReflectionKeyedEp), typeof(EmptyRequest), typeof(EmptyResponse));

        var props = epDef.ServiceBoundEpProps;

        props.Length.ShouldBe(2);
        props.ShouldContain(p => p.PropertyInfo.Name == nameof(ReflectionKeyedEp.ServiceA) && p.ServiceKey == "KEY_A");
        props.ShouldContain(p => p.PropertyInfo.Name == nameof(ReflectionKeyedEp.ServiceB) && p.ServiceKey == null);
    }

    [Fact]
    public void Source_Gen_Path_Returns_Service_Keys_From_Cache()
    {
        // Simulate what the source generator does: populate ReflectionCache for the endpoint
        // type with PropertyDefinition entries that carry ServiceKey values.
        var epType = typeof(SourceGenKeyedEp);
        var propA = epType.GetProperty(nameof(SourceGenKeyedEp.ServiceA))!;
        var propB = epType.GetProperty(nameof(SourceGenKeyedEp.ServiceB))!;

        Config.BndOpts.ReflectionCache.TryAdd(
            epType,
            new TypeDefinition
            {
                Properties = new(
                [
                    new(propA, new PropertyDefinition { ServiceKey = "CACHED_A" }),
                    new(propB, new PropertyDefinition { ServiceKey = "CACHED_B" })
                ])
            });

        var epDef = new EndpointDefinition(epType, typeof(EmptyRequest), typeof(EmptyResponse));

        var props = epDef.ServiceBoundEpProps;

        props.Length.ShouldBe(2);
        props.ShouldContain(p => p.PropertyInfo.Name == nameof(SourceGenKeyedEp.ServiceA) && p.ServiceKey == "CACHED_A");
        props.ShouldContain(p => p.PropertyInfo.Name == nameof(SourceGenKeyedEp.ServiceB) && p.ServiceKey == "CACHED_B");
    }
}

// Unique file-scoped types ensure no cross-test ReflectionCache pollution.

file sealed class ReflectionKeyedEp
{
    [KeyedService("KEY_A")]
    public object ServiceA { get; set; } = default!;

    public object ServiceB { get; set; } = default!; // intentionally no key
}

file sealed class SourceGenKeyedEp
{
    public object ServiceA { get; set; } = default!;
    public object ServiceB { get; set; } = default!;
}
