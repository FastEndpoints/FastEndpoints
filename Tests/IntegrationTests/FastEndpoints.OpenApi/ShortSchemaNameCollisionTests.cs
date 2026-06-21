using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi
{
    public class ShortSchemaNameCollisionTests
    {
        [Fact]
        public void short_schema_name_collisions_get_deterministic_suffixes_without_prescan()
        {
            var registry = new SchemaNameRegistry();

            var alphaRefId = SchemaNameGenerator.GetReferenceId(typeof(CollisionAlpha.Thing), shortSchemaNames: true, registry);
            var betaRefId = SchemaNameGenerator.GetReferenceId(typeof(CollisionBeta.Thing), shortSchemaNames: true, registry);

            alphaRefId.ShouldBe("Thing");
            betaRefId.ShouldBe("Thing2");
            SchemaNameGenerator.GetReferenceId(typeof(CollisionAlpha.Thing), shortSchemaNames: true, registry).ShouldBe(alphaRefId);
            SchemaNameGenerator.GetReferenceId(typeof(CollisionBeta.Thing), shortSchemaNames: true, registry).ShouldBe(betaRefId);
        }

        [Fact]
        public void nullable_short_schema_name_collisions_get_deterministic_suffixes_without_prescan()
        {
            var registry = new SchemaNameRegistry();

            var alphaRefId = SchemaNameGenerator.GetReferenceId(typeof(NullableCollisionAlpha.Thing?), shortSchemaNames: true, registry);
            var betaRefId = SchemaNameGenerator.GetReferenceId(typeof(NullableCollisionBeta.Thing?), shortSchemaNames: true, registry);

            alphaRefId.ShouldBe("NullableOfThing");
            betaRefId.ShouldBe("NullableOfThing2");
            SchemaNameGenerator.GetReferenceId(typeof(NullableCollisionAlpha.Thing?), shortSchemaNames: true, registry).ShouldBe(alphaRefId);
            SchemaNameGenerator.GetReferenceId(typeof(NullableCollisionBeta.Thing?), shortSchemaNames: true, registry).ShouldBe(betaRefId);
        }

        [Fact]
        public void shared_context_registers_colliding_short_schema_names_with_distinct_schema_refs()
        {
            var sharedCtx = new SharedContext();

            var alphaSchemaRef = typeof(CollisionAlpha.Thing).GetSchemaForType(sharedCtx, shortSchemaNames: true)
                                                            .ShouldBeOfType<OpenApiSchemaReference>();
            var betaSchemaRef = typeof(CollisionBeta.Thing).GetSchemaForType(sharedCtx, shortSchemaNames: true)
                                                          .ShouldBeOfType<OpenApiSchemaReference>();

            GetReferenceId(alphaSchemaRef).ShouldBe("Thing");
            GetReferenceId(betaSchemaRef).ShouldBe("Thing2");
            sharedCtx.MissingSchemaTypes["Thing"].ShouldBe(typeof(CollisionAlpha.Thing));
            sharedCtx.MissingSchemaTypes["Thing2"].ShouldBe(typeof(CollisionBeta.Thing));
        }

        [Fact]
        public void shared_context_registers_nullable_colliding_short_schema_names_as_underlying_types()
        {
            var sharedCtx = new SharedContext();

            var alphaSchemaRef = typeof(NullableCollisionAlpha.Thing?).GetSchemaForType(sharedCtx, shortSchemaNames: true)
                                                                     .ShouldBeOfType<OpenApiSchemaReference>();
            var betaSchemaRef = typeof(NullableCollisionBeta.Thing?).GetSchemaForType(sharedCtx, shortSchemaNames: true)
                                                                    .ShouldBeOfType<OpenApiSchemaReference>();

            GetReferenceId(alphaSchemaRef).ShouldBe("Thing");
            GetReferenceId(betaSchemaRef).ShouldBe("Thing2");
            sharedCtx.MissingSchemaTypes["Thing"].ShouldBe(typeof(NullableCollisionAlpha.Thing));
            sharedCtx.MissingSchemaTypes["Thing2"].ShouldBe(typeof(NullableCollisionBeta.Thing));
        }

        static string? GetReferenceId(OpenApiSchemaReference schemaRef)
            => schemaRef.Reference.Id ?? schemaRef.Id;
    }
}

namespace OpenApi.CollisionAlpha
{
    public record Thing(string A, int B);
}

namespace OpenApi.CollisionBeta
{
    public record Thing(bool X, double Y);
}

namespace OpenApi.NullableCollisionAlpha
{
    public readonly record struct Thing(string A, int B);
}

namespace OpenApi.NullableCollisionBeta
{
    public readonly record struct Thing(bool X, double Y);
}
