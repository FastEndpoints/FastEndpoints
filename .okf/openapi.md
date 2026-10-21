---
type: Reference
title: OpenAPI Contracts
description: Modern OpenAPI generation ownership, schema isolation, validation, examples, and compatibility traps.
tags: [api, architecture, gotcha]
---

# OpenAPI Contracts

Applies to `Src/OpenApi/`, the modern Microsoft.AspNetCore.OpenApi + Scalar path used by the harness. NSwag-based Swagger/ClientGen remain legacy; do not change their behavior implicitly. Runtime/package constraints: [dependencies.md](dependencies.md). Regression suites, host isolation, and snapshot regeneration: [testing.md](testing.md#openapi-regression-map).

## Generation and ownership
- Generation is per-request and concurrent. Do not cache or lock `GetOpenApiDocumentAsync`, or restore operation/schema bags to `SharedContext` (even with `AsyncLocal`). State belongs to `OpenApiGenerationState`, keyed by document in a `ConditionalWeakTable`. Shared TestServer execution contexts can let `ResetPerDocumentState()` clear sibling generations. Only snapshot helpers take `Fixture.DocumentGenerationLock`.
- `ValidationSchemaTransformer`: initialization, naming-policy-keyed rule cache, cache-construction scopes, and body/parameter orchestration.
- `ValidatorRuleTreeReader`: validator target discovery, naming-aware rule trees, and included-validator traversal for cached and live child validators. Its active-path type guard rejects include cycles but allows sibling reuse. Preserve `ChildValidatorAdaptor<,>` public methods for trimming.
- `ValidationSchemaApplier`: build live child trees; apply own rules before includes; traverse paths, check component eligibility, dispatch catalog mappings, and target child schemas. Reuse resolved property schemas and existing property-name item-localization keys with object fallback.
- `ChildValidatorResolver`: lazy scoped construction and a separate child-recursion guard. Invoke the application `Action<IValidator>` while guarded; release in `finally`, including on failure.
- `ValidationSchemaMutation`: reference/item localization and nullable-array preparation. Reuse the applier's single `OperationSchemaMutationContext`; delegate variant registration to `EnsureSchemaForMutation`. Generation state remains on `OpenApiGenerationState`.

## References and mutation
- `OpenApiSchemaReference` can expose null `Properties`/`Type` until `ResolveSchema()` (notably Microsoft.OpenApi 2.x). Supply `Components.Schemas` when `HostDocument` may be unset; cycle sets key on resolved schemas.
- Localize before mutation. Use `RetargetTo` or `KeepReferenceAnnotations`, not bare replacement references: preserve descriptions and clone the complete `Examples` list null-safely, in order. This includes alias rewrites and nullable-array items.
- `CloneSchemaMemberValue` copies discriminator `IDictionary<string, OpenApiSchemaReference>` mappings into independent ordinal dictionaries. Reference values remain shared, as in other clone helpers; replace them rather than mutating them.
- `CloneAsConcreteSchema` deep-copies `JsonNodeExtension` values and throws `NotSupportedException` for other `IOpenApiExtension` types. Nullable `oneOf: [null, $ref]` normalization clones the target before checking for arrays, so nullable enum/object components also take this path.
- `RequestOperationTransformer.ApplyBodySchemaMetadata` resolves properties first and localizes only changed descriptions or missing defaults. Compare property/reference-local descriptions before component summaries. Include `schemaRef.Reference.Description` in deduplication signatures so overrides cannot collapse into differently described shared schemas. Removed properties and existing defaults leave shared references intact.
- Normalize `IFormFile` and file collections to inline binary schemas before removing `IFormFile*` components, or references dangle.

## Validation rules
- `RuleForEach()` non-child validators target array `items`; `RuleFor()` still targets the collection. Resolve the framework's two-option null/array `oneOf`; clone referenced array branches per property and keep them concrete until nullable-array normalization. Preserve descriptions and existing item-localization keys. Leave multi-type unions unchanged.
- Item contexts retain their containing schema and explicit item target but set `RuleContext.RequiredPropertyOwner` to null. The catalog skips parent `Required` mutation for ownerless contexts while applying item value constraints. Other contexts default this owner to the containing schema.
- Compose `NotEmpty` with standalone/bounded/exact lengths independent of rule order, preserving existing minima; minimum-only rules leave maximum unset. Scalar-item and child rules must reuse one localized item variant in either order, preserving shared components.
- Skip a collection rule with `Filter` or `AsyncFilter` before applying any component, including child validators. Arbitrary item selection cannot constrain universal `items`. Inspect preserved `ICollectionRule<,>` properties without executing predicates; independent unfiltered rules remain active.
- Skip conditional non-child collection components before resolving items. Built-in presence, length, pattern, comparison/range, and email catalog mappings skip `RuleContext.HasCondition` (combined rule/component sync/async metadata). Preserve unconditional components, including `ApplyConditionTo.CurrentValidator` siblings. Keep condition handling in the catalog so custom mappings still see the metadata. Scalar/child traversal retains its existing behavior.
- Conditions apply to bodies and DTO-bound query/path/header/cookie parameters. Flattened `[FromQuery]` objects do not match nested validator paths.
- `RequestTransformState.ParametersBySchemaPath` uses canonical serialized DTO names and the validator `PropertyNameResolver`, never wire names or CLR aliases. `GetDictionaryOfRules` recovers CLR paths from expressions before resolving `JsonPropertyName` when the global resolver applied a naming policy. Preserve explicit overridden rule paths.
- `ParametersWithFallbackValues` preserves optional headers and supplementary query parameters for optional claims/permissions while constraints apply, even when `RemoveFromSchema` hides the body property. Explicit requiredness and ordinary single-source promotion remain. Cookie binding assigns even missing optional cookies, so presence validation can make a cookie indispensable.
- Optional `[FromClaim]` / `[HasPermission]` with `RemoveFromSchema` disappear from body schemas and query parameters in both OpenApi and Swagger. Header/cookie parameters stay documented; runtime binding is unchanged.

## Polymorphic unions
- Do not copy discriminator refs into `oneOf` during schema transformation: framework refs have null host documents and can throw on resolution (`UseOneOfForPolymorphism`, abstract `[JsonPolymorphic]`).
- After `DocumentSchemaNormalizer.Normalize`, `DocumentPolymorphicOneOf.Apply` binds mapping IDs present in `components.schemas`, carries unmapped `anyOf` branches (existing component refs or inline schemas), then clears `anyOf`. With no `anyOf`, or exactly the mapping set, the mapping alone supplies `oneOf`.
- For still-inline derived branches (same-status typed result/problem), promote them under matching mapping IDs in derived-type order using `TryAdd`. Leave the schema unchanged on missing component refs, ID collision, or unpairable branches. Never emit a ref to a missing component. If `oneOf` was already populated, leave `anyOf` in place. This bind is the sole `oneOf` membership writer; response media schemas stay component refs, not operation-local copies of `oneOf`.
- When `ValidationSchemaApplier.RecurseComposite` localizes a branch, retarget matching discriminator values with `RetargetTo`. Otherwise the mapping and localized `anyOf` become distinct alternatives and constraints leak. Lazily index mapping keys by target ID once per invocation, moving key lists as targets change. Preserve sequential repeated-ID semantics, case-sensitive aliases, and reference annotations across all three composite kinds.
- `DocumentPolymorphicOneOf.BindOneOf` currently emits one branch per mapping key, including duplicate targets for aliases. `PolymorphicValidationTests` deliberately preserves that behavior.

## Descriptions and examples
- Run `XmlDocSchemaTransformer` after `EnumSchemaTransformer`. For properties with `x-schema-id`, put XML summaries/examples in `Metadata["x-ref-description"]` / `Metadata["x-ref-example"]`; ASP.NET emits these beside `$ref`. Component descriptions belong to type summaries.
- `DocumentSchemaHelpers.AddMissingSchemas` materializes those annotations on nested inline schemas from `GetOrCreateSchemaAsync` using `OpenApiSchemaTraversal.Visit`. It shares child enumeration with reference collection and stops at component refs; replacement callers use `Rewrite`.
- Nullable collection normalization replaces `oneOf: [null, $ref]` with a `["null", "array"]` component clone. Preserve the reference option's description and full example list.
- `EnumSchemaTransformer` honors `JsonPropertyInfo.CustomConverter` using copied options and the declared property type (nullable converters are STJ wrappers). Remove `x-schema-id` and `x-is-nullable-property` for converter-backed enums to keep them inline; plain enums retain components. Nullable inline values include JSON null. Cloning/example matching must tolerate null entries; replace non-matching request examples with the first defined value.
- `EndpointSummary.ExampleRequest` / `RequestExamples` belong on request-body media `example` / `examples`, never schemas/properties. Endpoint-only example differences must not duplicate shared schemas. Preserve intentional XML/custom schema examples and real schema differences. Custom transformers read endpoint examples from operations. Regenerate clients and review names/signatures after changing this behavior.
- Use schema `Examples`, not obsolete `Example`, including XML and polymorphic generation. OpenAPI 3.1 emits arrays; 3.0 emits the first entry as singular `example`. Schema signatures include only `Examples`. Media, parameter, and header example APIs are separate and unchanged.
- `.http` bodies choose media `Example`, then first named `Examples`, then recursive schema placeholders. At each schema, prefer first reference-local `Examples`, then resolved concrete `Examples`, `Default`, or placeholders. Ignore obsolete concrete `Example`. Explicit null schema examples stay null in properties, never as a literal-null root body. Media examples replace the whole body without merging. Omit form bodies; use `{{body}}` for non-JSON.
- Bundled assemblies have empty `Location`: `XmlDocLookup` falls back to `<AssemblyName>.xml` in `AppContext.BaseDirectory`, where publishing places XML docs.

## Tags, security, and framework compatibility
- Keep every explicit `ITagsMetadata` name from root/nested groups and endpoints after stripping framework fallback tags. Deduplicate ordinally, preserving case variants. `AutoTagPathSegmentIndex <= 0` / `DontAutoTag()` suppress only route-derived tags. `TagCase` / `TagStripSymbols` affect automatic tags and `AutoTagOverride`, not explicit tags. Legacy Swagger is unchanged.
- Operation security scopes come only from explicit `DocumentOptions.AddAuth(..., globalScopeNames: ...)`, never `Roles()` / `AuthorizeAttribute.Roles`. Preserve scheme selection and anonymous exclusions. Legacy Swagger is unchanged.
- `OpenApiMediaTypeValue` aliases concrete media types on net10 (OpenAPI 2.x) and `IOpenApiMediaType` on net11 (3.x). `GetMutableMediaType(s)` localizes referenced media before schema/example mutation. XML signatures use `NodeType` on net11, not 2.x `Attribute` / `Wrapped`.
- On net11, `OpenApiDocument` registers `ResponseTypeCompatibilityProvider` once to preserve last-declared response types, media formats, and distinct default responses.
- Export mode keys belong to internal `OpenApiExportMode`; public `IsExportMode` / `IsNotExportMode` and per-format wrappers extend `IHost` / `IHostApplicationBuilder`.

## Sources
- `Src/OpenApi/`
- `Tests/IntegrationTests/FastEndpoints.OpenApi/`
- `Directory.Packages.props`
