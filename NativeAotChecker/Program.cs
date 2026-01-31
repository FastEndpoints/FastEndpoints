using System.Text.Json.Serialization;
using FastEndpoints.Security;
using NativeAotChecker;
using NativeAotChecker.Endpoints;

var bld = WebApplication.CreateSlimBuilder(args);
bld.Services
   .AddAuthenticationJwtBearer(o => o.SigningKey = bld.Configuration["Jwt-Secret"])
   .AddAuthorization()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddJobQueues<Job, JobStorage>();

// Register DI services for testing
bld.Services.AddScoped<IScopedCounter, ScopedCounter>();
bld.Services.AddSingleton<ISingletonService, SingletonService>();
bld.Services.AddTransient<ITransientService, TransientService>();
bld.Services.AddScoped<IAotTestService, AotTestService>();
bld.Services.AddScoped<IPropertyInjectedService, PropertyInjectedService>();

// Register Round 10 processors for AOT - required because AOT can't use ActivatorUtilities
bld.Services.AddSingleton<ValidationPreProcessor>();
bld.Services.AddSingleton<TimingPostProcessor>();

bld.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = bld.Build();
app.MapGet("healthy", () => Results.Ok());
app.UseAuthentication()
   .UseAuthorization()
   .UseFastEndpoints(c => c.Binding.ReflectionCache.AddFromNativeAotChecker());
app.UseJobQueues(o => o.StorageProbeDelay = TimeSpan.FromMilliseconds(50));
app.Run();

//needed by the hidden /_test_url_cache_ endpoint
[JsonSerializable(typeof(IEnumerable<string>))]
// Round 7 types
[JsonSerializable(typeof(ThrottleRequest))]
[JsonSerializable(typeof(ThrottleResponse))]
[JsonSerializable(typeof(IdempotencyRequest))]
[JsonSerializable(typeof(IdempotencyResponse))]
[JsonSerializable(typeof(ExpressionRouteRequest))]
[JsonSerializable(typeof(ExpressionRouteResponse))]
[JsonSerializable(typeof(CustomBindableId))]
[JsonSerializable(typeof(CustomBindingRequest))]
[JsonSerializable(typeof(CustomBindingResponse))]
[JsonSerializable(typeof(FileUploadStreamResponse))]
[JsonSerializable(typeof(ExtensionDataRequest))]
[JsonSerializable(typeof(ExtensionDataResponse))]
[JsonSerializable(typeof(ComputedPropertyRequest))]
[JsonSerializable(typeof(ComputedPropertyResponse))]
[JsonSerializable(typeof(CollectionTypesRequest))]
[JsonSerializable(typeof(CollectionTypesResponse))]
[JsonSerializable(typeof(ImmutableItem))]
[JsonSerializable(typeof(RequiredMembersRequest))]
[JsonSerializable(typeof(RequiredMembersResponse))]
[JsonSerializable(typeof(CrudResponse<ProductEntity>))]
[JsonSerializable(typeof(ProductEntity))]
[JsonSerializable(typeof(SealedRecordRequest))]
[JsonSerializable(typeof(SealedRecordResponse))]
[JsonSerializable(typeof(CustomConverterRequest))]
[JsonSerializable(typeof(CustomConverterResponse))]
[JsonSerializable(typeof(EnumSerializationRequest))]
[JsonSerializable(typeof(EnumSerializationResponse))]
[JsonSerializable(typeof(StatusWithCustomNames))]
[JsonSerializable(typeof(PermissionFlags))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(System.Dynamic.ExpandoObject))]
[JsonSerializable(typeof(HashSet<int>))]
[JsonSerializable(typeof(Queue<string>))]
[JsonSerializable(typeof(Stack<int>))]
[JsonSerializable(typeof(LinkedList<string>))]
[JsonSerializable(typeof(System.Collections.ObjectModel.ObservableCollection<int>))]
[JsonSerializable(typeof(SortedSet<string>))]
// Round 8 types - More JSON serialization patterns
[JsonSerializable(typeof(JsonPropertyNameRequest))]
[JsonSerializable(typeof(JsonPropertyNameResponse))]
[JsonSerializable(typeof(JsonNumberHandlingRequest))]
[JsonSerializable(typeof(JsonNumberHandlingResponse))]
[JsonSerializable(typeof(JsonConstructorRequest))]
[JsonSerializable(typeof(ImmutableRequest))]
[JsonSerializable(typeof(JsonConstructorResponse))]
[JsonSerializable(typeof(JsonDerivedTypeRequest))]
[JsonSerializable(typeof(JsonDerivedTypeResponse))]
[JsonSerializable(typeof(AnimalBase))]
[JsonSerializable(typeof(DogAnimal))]
[JsonSerializable(typeof(CatAnimal))]
[JsonSerializable(typeof(BirdAnimal))]
[JsonSerializable(typeof(PrivateSetterRequest))]
[JsonSerializable(typeof(InternalSetterRequest))]
[JsonSerializable(typeof(PrivateSetterResponse))]
[JsonSerializable(typeof(InitOnlyRequest))]
[JsonSerializable(typeof(NestedInitOnly))]
[JsonSerializable(typeof(RequiredInitRequest))]
[JsonSerializable(typeof(InitOnlyResponse))]
[JsonSerializable(typeof(RequiredInitResponse))]
[JsonSerializable(typeof(AnonymousTypeRequest))]
[JsonSerializable(typeof(LazyLoadingRequest))]
[JsonSerializable(typeof(LazyLoadingResponse))]
[JsonSerializable(typeof(FuncPropertyRequest))]
[JsonSerializable(typeof(FuncPropertyResponse))]
[JsonSerializable(typeof(ExpressionTreeRequest))]
[JsonSerializable(typeof(ExpressionTreeResponse))]
[JsonSerializable(typeof(TestItem))]
[JsonSerializable(typeof(ReflectionRequest))]
[JsonSerializable(typeof(ReflectionResponse))]
[JsonSerializable(typeof(ReflectionTestClass))]
[JsonSerializable(typeof(GenericMethodRequest))]
[JsonSerializable(typeof(GenericMethodResponse))]
// Round 9 types - Advanced serialization and runtime patterns
[JsonSerializable(typeof(ValueTupleRequest))]
[JsonSerializable(typeof(ValueTupleResponse))]
[JsonSerializable(typeof((string Name, int Age)))]
[JsonSerializable(typeof((int X, int Y, int Z)))]
[JsonSerializable(typeof((string, string, string)))]
[JsonSerializable(typeof(List<(int Id, string Value)>))]
[JsonSerializable(typeof(DateOnlyTimeOnlyRequest))]
[JsonSerializable(typeof(DateOnlyTimeOnlyResponse))]
[JsonSerializable(typeof(List<DateOnly>))]
[JsonSerializable(typeof(LargeNumericRequest))]
[JsonSerializable(typeof(LargeNumericResponse))]
[JsonSerializable(typeof(AsyncEnumerableItem))]
[JsonSerializable(typeof(List<AsyncEnumerableItem>))]
[JsonSerializable(typeof(IAsyncEnumerable<AsyncEnumerableItem>))]
[JsonSerializable(typeof(JsonDocumentRequest))]
[JsonSerializable(typeof(JsonDocumentResponse))]
[JsonSerializable(typeof(System.Text.Json.JsonDocument))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
[JsonSerializable(typeof(DelegateInvokeRequest))]
[JsonSerializable(typeof(DelegateInvokeResponse))]
[JsonSerializable(typeof(CovariantRequest))]
[JsonSerializable(typeof(CovariantResponse))]
[JsonSerializable(typeof(BaseEntity))]
[JsonSerializable(typeof(DerivedEntity))]
[JsonSerializable(typeof(List<BaseEntity>))]
[JsonSerializable(typeof(RecordCopyRequest))]
[JsonSerializable(typeof(RecordCopyResponse))]
[JsonSerializable(typeof(PersonRecord))]
[JsonSerializable(typeof(AddressRecord))]
[JsonSerializable(typeof(PatternMatchingRequest))]
[JsonSerializable(typeof(PatternMatchingResponse))]
[JsonSerializable(typeof(Shape))]
[JsonSerializable(typeof(CircleShape))]
[JsonSerializable(typeof(RectangleShape))]
[JsonSerializable(typeof(TriangleShape))]
[JsonSerializable(typeof(List<Shape>))]
[JsonSerializable(typeof(TimeProviderRequest))]
[JsonSerializable(typeof(TimeProviderResponse))]
[JsonSerializable(typeof(SourceDto))]
[JsonSerializable(typeof(DestinationDto))]
[JsonSerializable(typeof(ObjectMapperResponse))]
[JsonSerializable(typeof(FormattableStringRequest))]
[JsonSerializable(typeof(FormattableStringResponse))]
[JsonSerializable(typeof(StringComparisonRequest))]
[JsonSerializable(typeof(StringComparisonResponse))]
[JsonSerializable(typeof(ChannelRequest))]
[JsonSerializable(typeof(ChannelResponse))]
// Round 10 types - FastEndpoints features from documentation
[JsonSerializable(typeof(ProcessorStateRequest))]
[JsonSerializable(typeof(ProcessorStateResponse))]
[JsonSerializable(typeof(RequestStateBag))]
[JsonSerializable(typeof(FluentValidatorRequest))]
[JsonSerializable(typeof(FluentValidatorResponse))]
[JsonSerializable(typeof(FromClaimRequest))]
[JsonSerializable(typeof(FromClaimResponse))]
[JsonSerializable(typeof(PlainTextAotRequest))]
[JsonSerializable(typeof(PlainTextAotResponse))]
[JsonSerializable(typeof(ComplexQueryRequest))]
[JsonSerializable(typeof(ComplexQueryResponse))]
[JsonSerializable(typeof(SearchFilter))]
[JsonSerializable(typeof(GroupedEndpointRequest))]
[JsonSerializable(typeof(GroupedEndpointResponse))]
[JsonSerializable(typeof(DontBindRequest))]
[JsonSerializable(typeof(DontBindResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(TypedResultsUnionRequest))]
[JsonSerializable(typeof(TypedResultsUnionResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }