using System.Text.Json.Serialization;

// TestCases namespace aliases - Binding
using RouteBindingTest = TestCases.RouteBindingTest;
using CustomRequestBinder = TestCases.CustomRequestBinder;
using DupeParamBinding = TestCases.DupeParamBindingForIEnumerableProps;
using FormFileBindingTest = TestCases.FormFileBindingTest;
using JsonArrayIEnumerableProps = TestCases.JsonArrayBindingForIEnumerableProps;
using JsonArrayIEnumerableDto = TestCases.JsonArrayBindingToIEnumerableDto;
using JsonArrayListModels = TestCases.JsonArrayBindingToListOfModels;
using QueryObjectBinding = TestCases.QueryObjectBindingTest;
using QueryObjectArrayBinding = TestCases.QueryObjectWithObjectsArrayBindingTest;
using FormBindingComplex = TestCases.FormBindingComplexDtos;
using FromBodyBinding = TestCases.FromBodyJsonBinding;
using DontBindAttribute = TestCases.DontBindAttributeTest;
using FromCookieBinding = TestCases.FromCookieRequestBindingTest;
using StronglyTypedRoute = TestCases.StronglyTypedRouteParamTest;

// TestCases namespace aliases - Endpoints
using OnBeforeAfter = TestCases.OnBeforeAfterValidationTest;
using HydratedQueryParam = TestCases.HydratedQueryParamGeneratorTest;
using HydratedTestUrl = TestCases.HydratedTestUrlGeneratorTest;
using TypedResultTest = TestCases.TypedResultTest;
using PlainTextTest = TestCases.PlainTextRequestTest;
using EventStreamTest = TestCases.EventStreamTest;
using DontCatchExceptionsTest = TestCases.DontCatchExceptions;
using CacheBypassTest = TestCases.Endpoints.CacheBypassTest;
using IncludedValidatorTest = TestCases.IncludedValidatorTest;

// TestCases namespace aliases - Processors
using ProcessorStateTest = TestCases.ProcessorStateTest;
using PostProcessorTest = TestCases.PostProcessorTest;

// TestCases namespace aliases - Messaging
using ServerStreamingTest = TestCases.ServerStreamingTest;
using ClientStreamingTest = TestCases.ClientStreamingTest;
using CommandBusTest = TestCases.CommandBusTest;
using JobQueueTest = TestCases.JobQueueTest;
using EventBusTest = TestCases.EventBusTest;

// TestCases namespace aliases - Validation
using PreProcessorValidationFailure = TestCases.PreProcessorIsRunOnValidationFailure;
using DataAnnotationCompliant = TestCases.DataAnnotationCompliant;
using ValidationErrorTest = TestCases.ValidationErrorTest;

// TestCases namespace aliases - Security
using RateLimitTests = TestCases.RateLimitTests;
using MissingClaimTest = TestCases.MissingClaimTest;
using MissingHeaderTest = TestCases.MissingHeaderTest;

// TestCases namespace aliases - Misc
using MapperTest = TestCases.MapperTest;
using STJInfiniteRecursion = TestCases.STJInfiniteRecursionTest;
using UnitTestConcurrency = TestCases.UnitTestConcurrencyTest;
using GlobalRoutePrefixOverride = TestCases.GlobalRoutePrefixOverrideTest;

// TestCases namespace aliases - Routing
using RoutingTest = TestCases.Routing;

// TestCases namespace aliases - Idempotency
using IdempotencyTest = TestCases.Idempotency;

namespace Web.Serialization;

/// <summary>
/// Source-generated JSON serializer context for TestCases DTOs.
/// Separated from AppJsonContext to keep test types isolated from domain types.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNameCaseInsensitive = true)]

// ============================================================================
// EventHandling types
// ============================================================================
[JsonSerializable(typeof(TestCases.EventHandlingTest.SomeEvent))]

// ============================================================================
// TestCases DTOs - Antiforgery
// ============================================================================
[JsonSerializable(typeof(TestCases.AntiforgeryTest.TokenResponse), TypeInfoPropertyName = "AntiforgeryTokenResponse")]
[JsonSerializable(typeof(TestCases.AntiforgeryTest.VerificationRequest), TypeInfoPropertyName = "AntiforgeryVerificationRequest")]

// ============================================================================
// TestCases DTOs - Binding
// ============================================================================
// RouteBindingTest
[JsonSerializable(typeof(RouteBindingTest.Request), TypeInfoPropertyName = "RouteBindingRequest")]
[JsonSerializable(typeof(RouteBindingTest.Custom), TypeInfoPropertyName = "RouteBindingCustom")]
[JsonSerializable(typeof(RouteBindingTest.CustomList), TypeInfoPropertyName = "RouteBindingCustomList")]
[JsonSerializable(typeof(RouteBindingTest.Person), TypeInfoPropertyName = "RouteBindingPerson")]
[JsonSerializable(typeof(RouteBindingTest.NestedPerson), TypeInfoPropertyName = "RouteBindingNestedPerson")]

// CustomRequestBinderTest
[JsonSerializable(typeof(CustomRequestBinder.Request), TypeInfoPropertyName = "CustomRequestBinderRequest")]
[JsonSerializable(typeof(CustomRequestBinder.Response), TypeInfoPropertyName = "CustomRequestBinderResponse")]
[JsonSerializable(typeof(CustomRequestBinder.Product), TypeInfoPropertyName = "CustomRequestBinderProduct")]

// DupeParamBindingForIEnumerableProps
[JsonSerializable(typeof(DupeParamBinding.Request), TypeInfoPropertyName = "DupeParamBindingRequest")]
[JsonSerializable(typeof(DupeParamBinding.Response), TypeInfoPropertyName = "DupeParamBindingResponse")]
[JsonSerializable(typeof(DupeParamBinding.Request.Person), TypeInfoPropertyName = "DupeParamBindingPerson")]
[JsonSerializable(typeof(IEnumerable<DupeParamBinding.Request.Person>))]

// FormFileBindingTest (only non-IFormFile types for JSON)
[JsonSerializable(typeof(FormFileBindingTest.Response), TypeInfoPropertyName = "FormFileBindingResponse")]

// JsonArrayBindingForIEnumerableProps
[JsonSerializable(typeof(JsonArrayIEnumerableProps.Request), TypeInfoPropertyName = "JsonArrayIEnumerablePropsRequest")]
[JsonSerializable(typeof(JsonArrayIEnumerableProps.Response), TypeInfoPropertyName = "JsonArrayIEnumerablePropsResponse")]
[JsonSerializable(typeof(JsonArrayIEnumerableProps.Request.Person), TypeInfoPropertyName = "JsonArrayIEnumerablePropsPerson")]

// JsonArrayBindingToIEnumerableDto
[JsonSerializable(typeof(JsonArrayIEnumerableDto.Request), TypeInfoPropertyName = "JsonArrayIEnumerableDtoRequest")]
[JsonSerializable(typeof(JsonArrayIEnumerableDto.Item), TypeInfoPropertyName = "JsonArrayIEnumerableDtoItem")]
[JsonSerializable(typeof(JsonArrayIEnumerableDto.Response), TypeInfoPropertyName = "JsonArrayIEnumerableDtoResponse")]
[JsonSerializable(typeof(List<JsonArrayIEnumerableDto.Response>), TypeInfoPropertyName = "JsonArrayIEnumerableDtoResponseList")]

// JsonArrayBindingToListOfModels
[JsonSerializable(typeof(JsonArrayListModels.Request), TypeInfoPropertyName = "JsonArrayListModelsRequest")]
[JsonSerializable(typeof(JsonArrayListModels.Response), TypeInfoPropertyName = "JsonArrayListModelsResponse")]
[JsonSerializable(typeof(List<JsonArrayListModels.Request>))]
[JsonSerializable(typeof(List<JsonArrayListModels.Response>))]

// QueryObjectBindingTest
[JsonSerializable(typeof(QueryObjectBinding.Request), TypeInfoPropertyName = "QueryObjectBindingRequest")]
[JsonSerializable(typeof(QueryObjectBinding.Response), TypeInfoPropertyName = "QueryObjectBindingResponse")]
[JsonSerializable(typeof(QueryObjectBinding.Person), TypeInfoPropertyName = "QueryObjectBindingPerson")]
[JsonSerializable(typeof(QueryObjectBinding.NestedPerson), TypeInfoPropertyName = "QueryObjectBindingNestedPerson")]
[JsonSerializable(typeof(QueryObjectBinding.ByteEnum))]

// QueryObjectWithObjectsArrayBindingTest
[JsonSerializable(typeof(QueryObjectArrayBinding.Request), TypeInfoPropertyName = "QueryObjectArrayBindingRequest")]
[JsonSerializable(typeof(QueryObjectArrayBinding.Response), TypeInfoPropertyName = "QueryObjectArrayBindingResponse")]
[JsonSerializable(typeof(QueryObjectArrayBinding.Person), TypeInfoPropertyName = "QueryObjectArrayBindingPerson")]
[JsonSerializable(typeof(QueryObjectArrayBinding.NestedPerson), TypeInfoPropertyName = "QueryObjectArrayBindingNestedPerson")]
[JsonSerializable(typeof(QueryObjectArrayBinding.ObjectInArray), TypeInfoPropertyName = "QueryObjectArrayBindingObjectInArray")]
[JsonSerializable(typeof(List<QueryObjectArrayBinding.ObjectInArray>))]

// FormBindingComplexDtos
[JsonSerializable(typeof(FormBindingComplex.Book), TypeInfoPropertyName = "FormBindingBook")]
[JsonSerializable(typeof(FormBindingComplex.Author), TypeInfoPropertyName = "FormBindingAuthor")]
[JsonSerializable(typeof(FormBindingComplex.Address), TypeInfoPropertyName = "FormBindingAddress")]
[JsonSerializable(typeof(List<FormBindingComplex.Author>))]
[JsonSerializable(typeof(List<FormBindingComplex.Address>))]

// FromBodyJsonBinding
[JsonSerializable(typeof(FromBodyBinding.Request), TypeInfoPropertyName = "FromBodyBindingRequest")]
[JsonSerializable(typeof(FromBodyBinding.Response), TypeInfoPropertyName = "FromBodyBindingResponse")]
[JsonSerializable(typeof(FromBodyBinding.Product), TypeInfoPropertyName = "FromBodyBindingProduct")]

// DontBindAttributeTest
[JsonSerializable(typeof(DontBindAttribute.Request), TypeInfoPropertyName = "DontBindAttributeRequest")]
[JsonSerializable(typeof(DontBindAttribute.Response), TypeInfoPropertyName = "DontBindAttributeResponse")]

// FromCookieRequestBindingTest
[JsonSerializable(typeof(FromCookieBinding.Request), TypeInfoPropertyName = "FromCookieBindingRequest")]
[JsonSerializable(typeof(FromCookieBinding.Response), TypeInfoPropertyName = "FromCookieBindingResponse")]

// StronglyTypedRouteParamTest
[JsonSerializable(typeof(StronglyTypedRoute.Request), TypeInfoPropertyName = "StronglyTypedRouteRequest")]

// ============================================================================
// TestCases DTOs - Endpoints
// ============================================================================
// OnBeforeAfterTest
[JsonSerializable(typeof(OnBeforeAfter.Request), TypeInfoPropertyName = "OnBeforeAfterRequest")]
[JsonSerializable(typeof(OnBeforeAfter.Response), TypeInfoPropertyName = "OnBeforeAfterResponse")]

// HydratedQueryParamGeneratorTest
[JsonSerializable(typeof(HydratedQueryParam.Request), TypeInfoPropertyName = "HydratedQueryParamRequest")]
[JsonSerializable(typeof(HydratedQueryParam.Response), TypeInfoPropertyName = "HydratedQueryParamResponse")]
[JsonSerializable(typeof(HydratedQueryParam.Request.NestedClass))]
[JsonSerializable(typeof(HydratedQueryParam.Request.ComplexIdClass))]
[JsonSerializable(typeof(HydratedQueryParam.Request.ComplexIdClassWithToString))]

// HydratedTestUrlGeneratorTest
[JsonSerializable(typeof(HydratedTestUrl.Request), TypeInfoPropertyName = "HydratedTestUrlRequest")]

// TypedResultTest
[JsonSerializable(typeof(TypedResultTest.Request), TypeInfoPropertyName = "TypedResultRequest")]
[JsonSerializable(typeof(TypedResultTest.Response), TypeInfoPropertyName = "TypedResultResponse")]

// PlainTextRequestTest
[JsonSerializable(typeof(PlainTextTest.Request), TypeInfoPropertyName = "PlainTextRequest")]
[JsonSerializable(typeof(PlainTextTest.Response), TypeInfoPropertyName = "PlainTextResponse")]

// EventStreamTest
[JsonSerializable(typeof(EventStreamTest.Request), TypeInfoPropertyName = "EventStreamRequest")]
[JsonSerializable(typeof(EventStreamTest.SomeNotification), TypeInfoPropertyName = "EventStreamNotification")]
[JsonSerializable(typeof(EventStreamTest.SomeNotification[]))]

// DontCatchExceptions
[JsonSerializable(typeof(DontCatchExceptionsTest.Request), TypeInfoPropertyName = "DontCatchExceptionsRequest")]

// CacheBypassTest
[JsonSerializable(typeof(CacheBypassTest.Request), TypeInfoPropertyName = "CacheBypassRequest")]

// IncludedValidatorTest
[JsonSerializable(typeof(IncludedValidatorTest.Request), TypeInfoPropertyName = "IncludedValidatorRequest")]

// ============================================================================
// TestCases DTOs - Processors
// ============================================================================
// ProcessorStateTest
[JsonSerializable(typeof(ProcessorStateTest.Request), TypeInfoPropertyName = "ProcessorStateRequest")]

// PostProcessorTest
[JsonSerializable(typeof(PostProcessorTest.Request), TypeInfoPropertyName = "PostProcessorRequest")]
[JsonSerializable(typeof(PostProcessorTest.ExceptionDetailsResponse), TypeInfoPropertyName = "PostProcessorExceptionDetails")]

// GlobalGenericProcessorTest
[JsonSerializable(typeof(TestCases.GlobalGenericProcessorTest.Request), TypeInfoPropertyName = "GlobalGenericProcessorRequest")]

// ProcessorAttributesTest
[JsonSerializable(typeof(TestCases.ProcessorAttributesTest.Request), TypeInfoPropertyName = "ProcessorAttributesRequest")]
[JsonSerializable(typeof(List<string>), TypeInfoPropertyName = "ProcessorAttributesStringList")]

// PreProcessorShortWhileValidatorFails
[JsonSerializable(typeof(TestCases.PreProcessorShortWhileValidatorFails.Request), TypeInfoPropertyName = "PreProcessorShortValidatorRequest")]

// ============================================================================
// TestCases DTOs - Messaging
// ============================================================================
// ServerStreamingTest
[JsonSerializable(typeof(ServerStreamingTest.StatusStreamCommand), TypeInfoPropertyName = "ServerStreamingStatusCommand")]
[JsonSerializable(typeof(ServerStreamingTest.StatusUpdate), TypeInfoPropertyName = "ServerStreamingStatusUpdate")]

// ClientStreamingTest
[JsonSerializable(typeof(ClientStreamingTest.CurrentPosition), TypeInfoPropertyName = "ClientStreamingCurrentPosition")]
[JsonSerializable(typeof(ClientStreamingTest.ProgressReport), TypeInfoPropertyName = "ClientStreamingProgressReport")]

// CommandBusTest
[JsonSerializable(typeof(CommandBusTest.SomeCommand), TypeInfoPropertyName = "CommandBusSomeCommand")]
[JsonSerializable(typeof(CommandBusTest.EchoCommand), TypeInfoPropertyName = "CommandBusEchoCommand")]
[JsonSerializable(typeof(CommandBusTest.VoidCommand), TypeInfoPropertyName = "CommandBusVoidCommand")]

// JobQueueTest
[JsonSerializable(typeof(JobQueueTest.JobTestCommand), TypeInfoPropertyName = "JobQueueJobTestCommand")]
[JsonSerializable(typeof(JobQueueTest.JobCancelTestCommand), TypeInfoPropertyName = "JobQueueJobCancelCommand")]
[JsonSerializable(typeof(JobQueueTest.JobProgressTestCommand), TypeInfoPropertyName = "JobQueueJobProgressCommand")]

// EventBusTest
[JsonSerializable(typeof(EventBusTest.TestEventBus), TypeInfoPropertyName = "EventBusTestEvent")]

// ============================================================================
// TestCases DTOs - Validation
// ============================================================================
// PreProcessorIsRunOnValidationFailure
[JsonSerializable(typeof(PreProcessorValidationFailure.Request), TypeInfoPropertyName = "PreProcessorValidationFailureRequest")]
[JsonSerializable(typeof(PreProcessorValidationFailure.Response), TypeInfoPropertyName = "PreProcessorValidationFailureResponse")]

// DataAnnotationCompliant
[JsonSerializable(typeof(DataAnnotationCompliant.Request), TypeInfoPropertyName = "DataAnnotationRequest")]
[JsonSerializable(typeof(DataAnnotationCompliant.NestedRequest), TypeInfoPropertyName = "DataAnnotationNestedRequest")]
[JsonSerializable(typeof(DataAnnotationCompliant.ChildRequest), TypeInfoPropertyName = "DataAnnotationChildRequest")]
[JsonSerializable(typeof(List<DataAnnotationCompliant.ChildRequest>))]

// ValidationErrorTest
[JsonSerializable(typeof(ValidationErrorTest.ArrayRequest), TypeInfoPropertyName = "ValidationArrayRequest")]
[JsonSerializable(typeof(ValidationErrorTest.ListRequest), TypeInfoPropertyName = "ValidationListRequest")]
[JsonSerializable(typeof(ValidationErrorTest.DictionaryRequest), TypeInfoPropertyName = "ValidationDictionaryRequest")]
[JsonSerializable(typeof(ValidationErrorTest.ObjectArrayRequest), TypeInfoPropertyName = "ValidationObjectArrayRequest")]
[JsonSerializable(typeof(ValidationErrorTest.TObject), TypeInfoPropertyName = "ValidationTObject")]
[JsonSerializable(typeof(ValidationErrorTest.TObject[]))]

// ============================================================================
// TestCases DTOs - Security
// ============================================================================
// RateLimitTests
[JsonSerializable(typeof(RateLimitTests.Response), TypeInfoPropertyName = "RateLimitResponse")]

// MissingClaimTest
[JsonSerializable(typeof(MissingClaimTest.ThrowIfMissingRequest), TypeInfoPropertyName = "MissingClaimThrowRequest")]
[JsonSerializable(typeof(MissingClaimTest.DontThrowIfMissingRequest), TypeInfoPropertyName = "MissingClaimDontThrowRequest")]

// MissingHeaderTest
[JsonSerializable(typeof(MissingHeaderTest.ThrowIfMissingRequest), TypeInfoPropertyName = "MissingHeaderThrowRequest")]
[JsonSerializable(typeof(MissingHeaderTest.DontThrowIfMissingRequest), TypeInfoPropertyName = "MissingHeaderDontThrowRequest")]

// ============================================================================
// TestCases DTOs - Misc
// ============================================================================
// MapperTest
[JsonSerializable(typeof(MapperTest.Request), TypeInfoPropertyName = "MapperTestRequest")]
[JsonSerializable(typeof(MapperTest.Response), TypeInfoPropertyName = "MapperTestResponse")]
[JsonSerializable(typeof(MapperTest.Person), TypeInfoPropertyName = "MapperTestPerson")]

// STJInfiniteRecursionTest
[JsonSerializable(typeof(STJInfiniteRecursion.Response), TypeInfoPropertyName = "STJInfiniteRecursionResponse")]

// UnitTestConcurrencyTest
[JsonSerializable(typeof(UnitTestConcurrency.Request), TypeInfoPropertyName = "UnitTestConcurrencyRequest")]

// GlobalRoutePrefixOverrideTest
[JsonSerializable(typeof(GlobalRoutePrefixOverride.Request), TypeInfoPropertyName = "GlobalRoutePrefixRequest")]
[JsonSerializable(typeof(GlobalRoutePrefixOverride.Response), TypeInfoPropertyName = "GlobalRoutePrefixResponse")]

// ============================================================================
// TestCases DTOs - Routing
// ============================================================================
[JsonSerializable(typeof(RoutingTest.OptionalRouteParamTest.Request), TypeInfoPropertyName = "OptionalRouteParamRequest")]

// ============================================================================
// TestCases DTOs - Idempotency
// ============================================================================
[JsonSerializable(typeof(IdempotencyTest.Request), TypeInfoPropertyName = "IdempotencyRequest")]
[JsonSerializable(typeof(IdempotencyTest.Response), TypeInfoPropertyName = "IdempotencyResponse")]

// ============================================================================
// TestCases DTOs - Endpoints without Request (binding tests)
// ============================================================================
[JsonSerializable(typeof(TestCases.RouteBindingInEpWithoutReq.Response), TypeInfoPropertyName = "RouteBindingInEpWithoutReqResponse")]
[JsonSerializable(typeof(TestCases.QueryParamBindingInEpWithoutReq.Response), TypeInfoPropertyName = "QueryParamBindingInEpWithoutReqResponse")]

// ============================================================================
// TestCases DTOs - CommandBus
// ============================================================================
[JsonSerializable(typeof(CommandBusTest.ReceiverRequest), TypeInfoPropertyName = "CommandBusReceiverRequest")]

internal sealed partial class TestCasesJsonContext : JsonSerializerContext { }
