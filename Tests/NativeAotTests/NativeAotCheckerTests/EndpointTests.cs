using System.Net.Http.Json;
using NativeAotChecker.Endpoints;

namespace NativeAotCheckerTests;

public class EndpointTests(App app)
{
    /// <summary>
    /// Tests basic JSON serialization/deserialization in AOT mode.
    /// AOT ISSUE: System.Text.Json source generation is required for AOT. Without proper JsonSerializerContext,
    /// the runtime cannot serialize/deserialize types as reflection-based serialization is trimmed away.
    /// </summary>
    [Fact]
    public async Task Json_Serialization_With_Http_Post()
    {
        var (rsp, res) = await app.Client.POSTAsync<JsonPostEndpoint, JsonPostRequest, JsonPostResponse>(
                             new()
                             {
                                 FirstName = "Jane",
                                 LastName = "Doe"
                             });

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Message.ShouldBe("Hello Jane Doe!");
    }

    /// <summary>
    /// Tests non-JSON binding (route, query params) with custom value types in AOT mode.
    /// AOT ISSUE: Custom value types like UserName require IParsable<T> implementation which may need
    /// reflection to discover the Parse method. Guid binding also requires runtime type conversion.
    /// </summary>
    [Fact]
    public async Task Non_Json_Binding()
    {
        var id = Guid.NewGuid();
        var req = new NonJsonRequest
        {
            Id = id.ToString(),
            Identifier = id,
            UserName = new()
            {
                Value = id.ToString()
            }
        };
        var (rsp, res) = await app.Client.GETAsync<NonJsonBindingEndpoint, NonJsonRequest, NonJsonResponse>(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeEquivalentTo(
            new NonJsonResponse
            {
                Id = id.ToString(),
                Identifier = req.Identifier,
                UserName = req.UserName.Value
            });
    }

    /// <summary>
    /// Tests command handler execution with results in AOT mode.
    /// AOT ISSUE: Command handlers use generic ICommandHandler<TCommand, TResult> which requires runtime
    /// type resolution. The DI container must instantiate handlers using reflection-free patterns.
    /// </summary>
    [Fact]
    public async Task Command_Execution_With_Result()
    {
        var (rsp, res, err) = await app.Client.GETAsync<CommandExecutionEndpoint, CommandExecutionRequest, string>(new() { Name = "IRIS" });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe("SIRI");
        else
            Assert.Fail(err);
    }

    /// <summary>
    /// Tests event publishing with IEventHandler<TEvent> in AOT mode.
    /// AOT ISSUE: Event handlers are discovered and invoked via reflection. Multiple handlers for the same
    /// event type require runtime type scanning which is unavailable in trimmed AOT builds.
    /// </summary>
    [Fact]
    public async Task Event_Publish()
    {
        var id = Guid.NewGuid();
        var (rsp, res, err) = await app.Client.POSTAsync<EventPublishEndpoint, EventPublishRequest, Guid>(new() { Id = id });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe(id);
        else
            Assert.Fail(err);
    }

    /// <summary>
    /// Tests job queue with results in AOT mode.
    /// AOT ISSUE: Job queues use IJobStorageProvider<TJob> and background processing which requires
    /// dynamic type instantiation. Job results are tracked via generic dictionaries that need type metadata.
    /// </summary>
    [Fact]
    public async Task Queue_Jobs_That_Return_Results()
    {
        var ids = new List<(Guid id, Task<TestResult<string>> responseTask)>();

        for (var i = 0; i < 100; i++)
        {
            var id = Guid.NewGuid();
            var task = app.Client.GETAsync<JobQueueEndpoint, JobQueueRequest, string>(new() { Id = id });
            ids.Add((id, task));
        }

        var results = await Task.WhenAll(ids.Select(i => i.responseTask));

        for (var i = 0; i < 100; i++)
        {
            var (id, _) = ids[i];
            var (rsp, res, err) = results[i];

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.ShouldBe(id.ToString());
        }
    }

    /// <summary>
    /// Tests binding from multiple sources (JSON, form, headers, claims) in AOT mode.
    /// AOT ISSUE: Multi-source binding requires runtime inspection of [FromHeader], [FromClaim], [FromForm]
    /// attributes to determine which property binds from which source. Attribute-based discovery is trimmed.
    /// </summary>
    [Fact]
    public async Task Multi_Source_Binding()
    {
        var (_, token) = await app.Client.GETAsync<GetJwtTokenEndpoint, string>();

        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var req = new MultiSourceBindingRequest
        {
            Description = "Test description from JSON",
            Id = 456,
            Category = "test-category",
            FormValue = "Hello from form",
            RequestId = "test-req-789"
        };

        var (rsp, res, err) =
            await app.Client.POSTAsync<MultiSourceBindingEndpoint, MultiSourceBindingRequest, MultiSourceBindingResponse>(req, sendAsFormData: true);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Description.ShouldBe("Test description from JSON");
        res.Id.ShouldBe(456);
        res.Category.ShouldBe("test-category");
        res.UserId.ShouldBe("001");
        res.FormValue.ShouldBe("Hello from form");
        res.RequestId.ShouldBe("test-req-789");
    }

    /// <summary>
    /// Tests complex object binding from query string in AOT mode.
    /// AOT ISSUE: Complex query binding uses reflection to recursively bind nested object properties
    /// from flattened query parameters (e.g., Person.Name=John). Property discovery is reflection-based.
    /// </summary>
    [Fact]
    public async Task Complex_Object_Query_Binding()
    {
        var id = Guid.NewGuid();
        var req = new ComplexObjectQueryBindingRequest
        {
            Person = new()
            {
                Id = id,
                Name = "John Doe",
                Age = 30
            },
            Category = "test-category"
        };

        var (rsp, res, err) = await app.Client.GETAsync<ComplexObjectQueryBindingEndpoint, ComplexObjectQueryBindingRequest, ComplexObjectQueryBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Person.Id.ShouldBe(id);
        res.Person.Name.ShouldBe("John Doe");
        res.Person.Age.ShouldBe(30);
        res.Category.ShouldBe("test-category");
    }

    // ==================== NEW AOT TESTS ====================

    /// <summary>
    /// Tests FluentValidation validators pass in AOT mode.
    /// AOT ISSUE: FluentValidation uses expression trees and reflection to build validation rules.
    /// RuleFor(x => x.Property) requires runtime expression compilation which may fail in AOT.
    /// </summary>
    [Fact]
    public async Task Validator_Passes_In_AOT_Mode()
    {
        var req = new ValidatorRequest
        {
            Name = "John Doe",
            Age = 30,
            Email = "john@example.com"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ValidatorEndpoint, ValidatorRequest, ValidatorResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IsValid.ShouldBeTrue();
        res.Message.ShouldContain("John Doe");
    }

    /// <summary>
    /// Tests FluentValidation validator failures return proper errors in AOT mode.
    /// AOT ISSUE: Validation error messages use property names via reflection (nameof alternative needed).
    /// Error serialization requires the ErrorResponse type to be in JsonSerializerContext.
    /// </summary>
    [Fact]
    public async Task Validator_Fails_In_AOT_Mode()
    {
        var req = new ValidatorRequest
        {
            Name = "", // Invalid: empty
            Age = 200, // Invalid: out of range
            Email = "not-an-email" // Invalid: not email format
        };

        var (rsp, _, _) = await app.Client.POSTAsync<ValidatorFailureEndpoint, ValidatorRequest, ValidatorResponse>(req);

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests IPreProcessor<TRequest> execution in AOT mode.
    /// AOT ISSUE: Pre-processors are instantiated via DI using Activator.CreateInstance or similar.
    /// Generic pre-processors IPreProcessor<T> require runtime generic type instantiation.
    /// </summary>
    [Fact]
    public async Task Pre_Processor_Works_In_AOT_Mode()
    {
        var req = new ProcessorTestRequest { Input = "test-input" };

        var (rsp, res, err) = await app.Client.POSTAsync<ProcessorTestEndpoint, ProcessorTestRequest, ProcessorTestResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Input.ShouldBe("test-input");
        res.PreProcessorRan.ShouldBeTrue();
        res.ProcessedBy.ShouldBe("AotPreProcessor");
    }

    /// <summary>
    /// Tests IPostProcessor<TRequest, TResponse> execution in AOT mode.
    /// AOT ISSUE: Post-processors with generic constraints require runtime type matching.
    /// The processor pipeline uses reflection to invoke processors in the correct order.
    /// </summary>
    [Fact]
    public async Task Post_Processor_Works_In_AOT_Mode()
    {
        // First, make a request to trigger the post-processor
        var req = new ProcessorTestRequest { Input = "post-processor-test" };
        var (rsp, _, err) = await app.Client.POSTAsync<ProcessorTestEndpoint, ProcessorTestRequest, ProcessorTestResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        // Then verify the post-processor ran
        var (verifyRsp, verifyRes, verifyErr) = await app.Client.GETAsync<VerifyProcessorEndpoint, VerifyProcessorResponse>();

        if (!verifyRsp.IsSuccessStatusCode)
            Assert.Fail(verifyErr);

        verifyRes.PostProcessorRan.ShouldBeTrue();
        verifyRes.LastInput.ShouldBe("post-processor-test");
    }

    /// <summary>
    /// Tests Mapper<TRequest, TResponse, TEntity> in AOT mode.
    /// AOT ISSUE: Mapper uses generic base class with virtual methods and entity mapping.
    /// The mapper instantiation uses reflection: typeof(Mapper<,,>).MakeGenericType() fails in AOT.
    /// Entity-to-DTO mapping uses property reflection for automatic mapping.
    /// </summary>
    [Fact] // AOT ISSUE: Mapper<TRequest, TResponse, TEntity> doesn't work in Native AOT mode
    public async Task Mapper_Works_In_AOT_Mode()
    {
        var req = new MapperRequest
        {
            FirstName = "John",
            LastName = "Doe",
            BirthYear = 1990
        };

        var (rsp, res, err) = await app.Client.POSTAsync<MapperEndpoint, MapperRequest, MapperResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FullName.ShouldBe("John Doe");
        res.Age.ShouldBeGreaterThan(30); // Born in 1990
        res.EntityId.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests multiple IEventHandler<TEvent> for same event type in AOT mode.
    /// AOT ISSUE: Multiple handlers are discovered via assembly scanning and reflection.
    /// Handler ordering and parallel execution require runtime type enumeration.
    /// </summary>
    [Fact]
    public async Task Multiple_Event_Handlers_Work_In_AOT_Mode()
    {
        var id = Guid.NewGuid();
        var req = new MultiEventHandlerRequest { Id = id };

        var (rsp, res, err) = await app.Client.POSTAsync<MultiEventHandlerEndpoint, MultiEventHandlerRequest, MultiEventHandlerResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Id.ShouldBe(id);
        res.HandlerCount.ShouldBe(3);
        res.HandlersExecuted.ShouldContain("Handler1");
        res.HandlersExecuted.ShouldContain("Handler2");
        res.HandlersExecuted.ShouldContain("Handler3");
    }

    /// <summary>
    /// Tests enum binding from route/query parameters in AOT mode.
    /// AOT ISSUE: Enum.Parse<T> and Enum.TryParse<T> use reflection to get enum values.
    /// JsonStringEnumConverter requires enum type metadata which may be trimmed.
    /// </summary>
    [Fact]
    public async Task Enum_Binding_Works_In_AOT_Mode()
    {
        var req = new EnumBindingRequest
        {
            Category = ProductCategory.Electronics,
            Status = OrderStatus.Shipped,
            OptionalCategory = ProductCategory.Books
        };

        var (rsp, res, err) = await app.Client.GETAsync<EnumBindingEndpoint, EnumBindingRequest, EnumBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Category.ShouldBe(ProductCategory.Electronics);
        res.CategoryName.ShouldBe("Electronics");
        res.Status.ShouldBe(OrderStatus.Shipped);
        res.StatusName.ShouldBe("Shipped");
        res.OptionalCategory.ShouldBe(ProductCategory.Books);
    }

    /// <summary>
    /// Tests List<T>, array binding from query parameters in AOT mode.
    /// AOT ISSUE: Collection binding requires creating List<T> instances via Activator.CreateInstance.
    /// Element type parsing uses TypeConverter which relies on reflection.
    /// </summary>
    [Fact]
    public async Task Collection_Binding_Works_In_AOT_Mode()
    {
        var req = new CollectionBindingRequest
        {
            Ids = [1, 2, 3, 4, 5],
            Names = ["Alice", "Bob", "Charlie"],
            Guids = [Guid.NewGuid(), Guid.NewGuid()],
            Categories = [ProductCategory.Electronics, ProductCategory.Clothing]
        };

        var (rsp, res, err) = await app.Client.GETAsync<CollectionBindingEndpoint, CollectionBindingRequest, CollectionBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IdCount.ShouldBe(5);
        res.NameCount.ShouldBe(3);
        res.GuidCount.ShouldBe(2);
        res.CategoryCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests deeply nested object binding with dictionaries in AOT mode.
    /// AOT ISSUE: Nested object instantiation uses recursive reflection to create object graph.
    /// Dictionary<string, T> deserialization requires runtime generic instantiation.
    /// </summary>
    [Fact]
    public async Task Nested_Object_Binding_Works_In_AOT_Mode()
    {
        var req = new NestedObjectRequest
        {
            Name = "Test User",
            Contact = new ContactInfo
            {
                Email = "test@example.com",
                Phone = "123-456-7890",
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Springfield",
                    ZipCode = "12345",
                    Country = new Country
                    {
                        Name = "United States",
                        Code = "US"
                    }
                }
            },
            Tags = ["tag1", "tag2", "tag3"],
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<NestedObjectEndpoint, NestedObjectRequest, NestedObjectResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("Test User");
        res.Email.ShouldBe("test@example.com");
        res.Street.ShouldBe("123 Main St");
        res.CountryCode.ShouldBe("US");
        res.TagCount.ShouldBe(3);
        res.MetadataCount.ShouldBe(2);
    }

    /// <summary>
    /// Tests dependency injection (scoped, singleton, transient) in AOT mode.
    /// AOT ISSUE: DI container uses reflection to discover constructors and inject dependencies.
    /// Generic services IService<T> require runtime generic instantiation.
    /// </summary>
    [Fact]
    public async Task Dependency_Injection_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<DiTestEndpoint, DiTestResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        // Scoped counter was incremented 3 times
        res.ScopedCounterValue.ShouldBe(3);
        
        // Singleton should have a consistent ID
        res.SingletonInstanceId.ShouldNotBe(Guid.Empty);
        
        // Transient services should have different IDs
        res.TransientIdsAreDifferent.ShouldBeTrue();
    }

    /// <summary>
    /// Tests TypedResults.Ok<T> return type in AOT mode.
    /// AOT ISSUE: TypedResults use IResult implementations that serialize response via reflection.
    /// The generic Ok<T> needs runtime type information for JSON serialization.
    /// </summary>
    [Fact]
    public async Task Typed_Result_Ok_Works_In_AOT_Mode()
    {
        var req = new TypedResultRequest { StatusCode = 200, Message = "Success!" };

        var (rsp, res, err) = await app.Client.GETAsync<TypedResultEndpoint, TypedResultRequest, TypedResultResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Message.ShouldBe("Success!");
    }

    /// <summary>
    /// Tests TypedResults.NoContent() return type in AOT mode.
    /// AOT ISSUE: TypedResults must be properly registered for AOT. Even parameterless results
    /// require the IResult implementation to be preserved from trimming.
    /// </summary>
    [Fact]
    public async Task Typed_Result_NoContent_Works_In_AOT_Mode()
    {
        var req = new TypedResultRequest { StatusCode = 204 };

        var (rsp, _, _) = await app.Client.GETAsync<TypedResultEndpoint, TypedResultRequest, TypedResultResponse>(req);

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Tests generic command handlers ICommandHandler<TCommand, TResult> in AOT mode.
    /// AOT ISSUE: Generic command handlers require source generator to pre-generate handler bindings.
    /// Without AOT-specific code gen, the runtime cannot resolve ICommandHandler<,> implementations.
    /// MakeGenericType() calls fail as generic type instantiation is not available at runtime.
    /// </summary>
    [Fact] // AOT ISSUE: Generic command handlers require source generator changes for AOT support
    public async Task Generic_Command_Handler_Works_In_AOT_Mode()
    {
        var req = new GenericCommandRequest
        {
            OperationType = "Create",
            ProductName = "Test Product",
            ProductPrice = 99.99m
        };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericCommandEndpoint, GenericCommandRequest, GenericCommandResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Success.ShouldBeTrue();
        res.OperationType.ShouldBe("Create");
        res.ProductName.ShouldBe("Test Product");
        res.ProductPrice.ShouldBe(99.99m);
        res.HandlerType.ShouldContain("AotGenericCommandHandler");
    }

    /// <summary>
    /// Tests generic pre-processor IPreProcessor<T> in AOT mode.
    /// AOT ISSUE: Generic processors use open generic types resolved at runtime.
    /// typeof(IPreProcessor<>).MakeGenericType(requestType) is not AOT compatible.
    /// </summary>
    [Fact]
    public async Task Generic_Pre_Processor_Works_In_AOT_Mode()
    {
        var req = new GenericProcessorRequest { Input = "generic-test", Value = 42 };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericProcessorEndpoint, GenericProcessorRequest, GenericProcessorResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Input.ShouldBe("generic-test");
        res.Value.ShouldBe(42);
        res.GenericPreProcessorRan.ShouldBeTrue();
        res.PreProcessorRequestType.ShouldBe("GenericProcessorRequest");
    }

    /// <summary>
    /// Tests generic post-processor IPostProcessor<TReq, TRes> in AOT mode.
    /// AOT ISSUE: Two generic type parameters require double generic instantiation at runtime.
    /// Both request and response types must be known at compile time for AOT.
    /// </summary>
    [Fact]
    public async Task Generic_Post_Processor_Works_In_AOT_Mode()
    {
        // First, trigger the generic post-processor
        var req = new GenericProcessorRequest { Input = "generic-post-test", Value = 100 };
        var (rsp, _, err) = await app.Client.POSTAsync<GenericProcessorEndpoint, GenericProcessorRequest, GenericProcessorResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        // Verify the generic post-processor ran
        var (verifyRsp, verifyRes, verifyErr) = await app.Client.GETAsync<VerifyGenericProcessorEndpoint, VerifyGenericProcessorResponse>();

        if (!verifyRsp.IsSuccessStatusCode)
            Assert.Fail(verifyErr);

        verifyRes.PostProcessorRan.ShouldBeTrue();
        verifyRes.LastRequestType.ShouldBe("GenericProcessorRequest");
        verifyRes.LastResponseType.ShouldBe("GenericProcessorResponse");
    }

    // ==================== ADDITIONAL AOT TESTS ====================

    /// <summary>
    /// Tests [FromHeader] attribute binding in AOT mode.
    /// AOT ISSUE: [FromHeader] binding uses reflection to find properties with the attribute.
    /// GetCustomAttribute<FromHeaderAttribute>() scans for metadata that may be trimmed.
    /// Header name-to-property mapping requires runtime reflection.
    /// </summary>
    [Fact] // AOT ISSUE: FromHeader binding does not work in Native AOT mode
    public async Task Header_Binding_Works_In_AOT_Mode()
    {
        var correlationId = Guid.NewGuid().ToString();
        var tenantId = "tenant-123";

        app.Client.DefaultRequestHeaders.Add("x-correlation-id", correlationId);
        app.Client.DefaultRequestHeaders.Add("x-tenant-id", tenantId);

        try
        {
            var (rsp, res, err) = await app.Client.GETAsync<HeaderBindingEndpoint, HeaderBindingRequest, HeaderBindingResponse>(new());

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.CorrelationId.ShouldBe(correlationId);
            res.TenantId.ShouldBe(tenantId);
            res.AllHeadersBound.ShouldBeTrue();
        }
        finally
        {
            app.Client.DefaultRequestHeaders.Remove("x-correlation-id");
            app.Client.DefaultRequestHeaders.Remove("x-tenant-id");
        }
    }

    /// <summary>
    /// Tests EndpointWithoutRequest base class in AOT mode.
    /// AOT ISSUE: Empty request types use EmptyRequest internally which must be in JsonSerializerContext.
    /// The endpoint type hierarchy must be preserved during trimming.
    /// </summary>
    [Fact]
    public async Task Endpoint_Without_Request_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<EndpointWithoutRequestEndpoint, NoRequestResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Message.ShouldNotBeNullOrEmpty();
        res.ServerName.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests endpoint with no request and no response body in AOT mode.
    /// AOT ISSUE: Minimal endpoints still need Configure() method preserved.
    /// NoContent responses must have IResult implementation available.
    /// </summary>
    [Fact]
    public async Task Endpoint_Without_Request_No_Response_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("no-request-no-response-endpoint");
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Tests OnBeforeValidate/OnAfterValidate lifecycle hooks in AOT mode.
    /// AOT ISSUE: Virtual method dispatch for lifecycle hooks requires method table preservation.
    /// Override methods in derived endpoint classes may be trimmed if not explicitly preserved.
    /// </summary>
    [Fact]
    public async Task OnBeforeValidate_OnAfterValidate_Work_In_AOT_Mode()
    {
        var req = new LifecycleHooksRequest { Input = "test-input" };

        var (rsp, res, err) = await app.Client.POSTAsync<OnBeforeAfterEndpoint, LifecycleHooksRequest, LifecycleHooksResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Input.ShouldBe("test-input");
        res.BeforeValidateRan.ShouldBeTrue();
        res.AfterValidateRan.ShouldBeTrue();
        res.TransformedByBefore.ShouldBe("BEFORE:test-input");
        res.TransformedByAfter.ShouldBe("AFTER:test-input");
    }

    /// <summary>
    /// Tests AddError/ThrowIfAnyErrors manual validation in AOT mode.
    /// AOT ISSUE: ValidationFailure creation uses property names which may need nameof().
    /// Error collection serialization requires proper JsonSerializerContext registration.
    /// </summary>
    [Fact]
    public async Task Manual_Validation_AddError_Works_In_AOT_Mode()
    {
        var req = new ManualValidationRequest
        {
            Username = "TestUser",
            Age = 25,
            ShouldFail = false
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ManualValidationEndpoint, ManualValidationRequest, ManualValidationResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ValidationPassed.ShouldBeTrue();
        res.Message.ShouldContain("TestUser");
    }

    /// <summary>
    /// Tests manual validation failure response in AOT mode.
    /// AOT ISSUE: Bad request response with validation errors requires ErrorResponse in context.
    /// HTTP 400 response body serialization needs type metadata preserved.
    /// </summary>
    [Fact]
    public async Task Manual_Validation_AddError_Fails_In_AOT_Mode()
    {
        var req = new ManualValidationRequest
        {
            Username = "",  // Empty - should fail
            Age = 200,      // Out of range - should fail
            ShouldFail = true
        };

        var (rsp, _, _) = await app.Client.POSTAsync<ManualValidationEndpoint, ManualValidationRequest, ManualValidationResponse>(req);

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests complex route constraints with multiple parameter types in AOT mode.
    /// AOT ISSUE: Route constraints use IRouteConstraint implementations resolved via reflection.
    /// Constraint types (int, guid, regex) require runtime type conversion.
    /// Complex route templates are parsed with reflection-based parameter binding.
    /// </summary>
    [Fact] // AOT ISSUE: Complex route constraints with multiple segments don't work in Native AOT mode
    public async Task Route_Constraints_Work_In_AOT_Mode()
    {
        var guidId = Guid.NewGuid();
        var req = new RouteConstraintRequest
        {
            IntId = 42,
            GuidId = guidId,
            StringId = "test-string",
            OptionalDouble = 3.14,
            OptionalBool = true
        };

        var (rsp, res, err) = await app.Client.GETAsync<RouteConstraintEndpoint, RouteConstraintRequest, RouteConstraintResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IntId.ShouldBe(42);
        res.GuidId.ShouldBe(guidId);
        res.StringId.ShouldBe("test-string");
        res.OptionalDouble.ShouldBe(3.14);
        res.OptionalBool.ShouldBe(true);
        res.AllConstraintsMet.ShouldBeTrue();
    }

    /// <summary>
    /// Tests optional route parameters in AOT mode.
    /// AOT ISSUE: Optional parameters require nullable type handling at runtime.
    /// Route parameter binding checks for null/default values using reflection.
    /// Default value assignment for missing optional params needs type metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Optional route parameters don't work in Native AOT mode
    public async Task Optional_Route_Parameter_Works_In_AOT_Mode()
    {
        // Test with optional param provided
        var req = new OptionalRouteRequest { RequiredId = 1, OptionalId = 99 };
        var (rsp, res, err) = await app.Client.GETAsync<OptionalRouteEndpoint, OptionalRouteRequest, OptionalRouteResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.RequiredId.ShouldBe(1);
        res.OptionalId.ShouldBe(99);
        res.OptionalWasProvided.ShouldBeTrue();
    }

    /// <summary>
    /// Tests PUT HTTP verb endpoint in AOT mode.
    /// AOT ISSUE: HTTP verb attribute [HttpPut] must be preserved during trimming.
    /// Request body binding for PUT uses JSON deserialization which needs context.
    /// </summary>
    [Fact]
    public async Task PUT_Verb_Works_In_AOT_Mode()
    {
        var req = new HttpVerbRequest { Id = 1, Data = "put-data" };
        var (rsp, res, err) = await app.Client.PUTAsync<PutVerbEndpoint, HttpVerbRequest, HttpVerbResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HttpMethod.ShouldBe("PUT");
        res.Data.ShouldBe("put-data");
    }

    /// <summary>
    /// Tests DELETE HTTP verb endpoint in AOT mode.
    /// AOT ISSUE: DELETE endpoints often use route-only binding without body.
    /// Empty request handling must work without reflection-based binding.
    /// </summary>
    [Fact]
    public async Task DELETE_Verb_Works_In_AOT_Mode()
    {
        var req = new HttpVerbRequest { Id = 1 };
        var (rsp, res, err) = await app.Client.DELETEAsync<DeleteVerbEndpoint, HttpVerbRequest, HttpVerbResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HttpMethod.ShouldBe("DELETE");
        res.Data.ShouldBe("Deleted");
    }

    /// <summary>
    /// Tests PATCH HTTP verb endpoint in AOT mode.
    /// AOT ISSUE: PATCH may use partial object binding which needs reflection.
    /// JsonPatch operations require dynamic property access.
    /// </summary>
    [Fact]
    public async Task PATCH_Verb_Works_In_AOT_Mode()
    {
        var req = new HttpVerbRequest { Id = 1, Data = "patch-data" };
        var (rsp, res, err) = await app.Client.PATCHAsync<PatchVerbEndpoint, HttpVerbRequest, HttpVerbResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HttpMethod.ShouldBe("PATCH");
        res.Data.ShouldBe("patch-data");
    }

    /// <summary>
    /// Tests HEAD HTTP verb endpoint in AOT mode.
    /// AOT ISSUE: HEAD responses have no body but headers must be preserved.
    /// Custom header writing needs IHeaderDictionary access preserved.
    /// </summary>
    [Fact]
    public async Task HEAD_Verb_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "http-verbs-head"));

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        rsp.Headers.Contains("X-Custom-Header").ShouldBeTrue();
    }

    /// <summary>
    /// Tests ResponseCache() configuration in AOT mode.
    /// AOT ISSUE: ResponseCache uses CacheProfile and cache headers set via reflection.
    /// IOutputCachePolicy implementation resolution requires reflection.
    /// Cache key generation may use property reflection for vary-by parameters.
    /// </summary>
    [Fact] // AOT ISSUE: ResponseCache configuration doesn't work in Native AOT mode
    public async Task Response_Caching_Configuration_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<ResponseCachingEndpoint, CachedResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Data.ShouldNotBeNullOrEmpty();
        res.UniqueId.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests multiple route definitions on single endpoint in AOT mode.
    /// AOT ISSUE: Multiple routes require endpoint registration for each route pattern.
    /// Route table building uses reflection to enumerate endpoint configurations.
    /// </summary>
    [Fact]
    public async Task Multiple_Routes_Work_In_AOT_Mode()
    {
        // Test route 1
        var rsp1 = await app.Client.GetAsync("multi-route-1?source=test1");
        rsp1.IsSuccessStatusCode.ShouldBeTrue();

        // Test route 2
        var rsp2 = await app.Client.GetAsync("multi-route-2?source=test2");
        rsp2.IsSuccessStatusCode.ShouldBeTrue();

        // Test route 3
        var rsp3 = await app.Client.GetAsync("multi-route-3?source=test3");
        rsp3.IsSuccessStatusCode.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Summary/Description/Tags OpenAPI configuration in AOT mode.
    /// AOT ISSUE: OpenAPI metadata uses attributes that may be trimmed.
    /// Swagger generation uses reflection to build API documentation.
    /// </summary>
    [Fact]
    public async Task Summary_And_Tags_Configuration_Works_In_AOT_Mode()
    {
        var req = new DocumentedRequest { Name = "John", Id = 123 };

        var (rsp, res, err) = await app.Client.POSTAsync<SummaryAndTagsEndpoint, DocumentedRequest, DocumentedResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("John");
        res.Id.ShouldBe(123);
        res.Message.ShouldContain("John");
    }

    /// <summary>
    /// Tests various HTTP status code responses in AOT mode.
    /// AOT ISSUE: Send.Ok(), Send.NotFound(), Send.NoContent() use IResult implementations.
    /// Each status code result type must be preserved during trimming.
    /// </summary>
    [Fact]
    public async Task Send_Various_Status_Codes_Work_In_AOT_Mode()
    {
        // Test 200 OK
        var rsp200 = await app.Client.GetAsync("send-status-code?code=200");
        rsp200.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // Test 204 No Content
        var rsp204 = await app.Client.GetAsync("send-status-code?code=204");
        rsp204.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);

        // Test 404 Not Found
        var rsp404 = await app.Client.GetAsync("send-status-code?code=404");
        rsp404.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests nullable value type binding (int?, double?, bool?, etc.) in AOT mode.
    /// AOT ISSUE: Nullable<T> requires special handling - Nullable.GetUnderlyingType() uses reflection.
    /// Binding nullable query/route params needs runtime type checking for null values.
    /// JSON deserialization of nullable properties requires type metadata preservation.
    /// </summary>
    [Fact] // AOT ISSUE: Nullable types binding doesn't work in Native AOT mode
    public async Task Nullable_Types_Binding_Works_In_AOT_Mode()
    {
        var req = new NullableTypesRequest
        {
            NullableInt = 42,
            NullableDouble = 3.14,
            NullableBool = true,
            NullableDateTime = new DateTime(2025, 1, 31, 12, 0, 0, DateTimeKind.Utc),
            NullableGuid = Guid.NewGuid(),
            NullableEnum = ProductCategory.Electronics,
            NullableString = "test",
            QueryNullableInt = 99
        };

        var (rsp, res, err) = await app.Client.POSTAsync<NullableTypesEndpoint, NullableTypesRequest, NullableTypesResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.NullableInt.ShouldBe(42);
        res.NullableDouble.ShouldBe(3.14);
        res.NullableBool.ShouldBe(true);
        res.NullableString.ShouldBe("test");
        res.NotNullCount.ShouldBe(8);
        res.NullCount.ShouldBe(0);
    }

    /// <summary>
    /// Tests nullable types with actual null values in AOT mode.
    /// AOT ISSUE: Null value handling requires default(T?) comparison which uses reflection.
    /// JSON null token parsing needs special AOT-compatible handling.
    /// </summary>
    [Fact]
    public async Task Nullable_Types_With_Nulls_Works_In_AOT_Mode()
    {
        var req = new NullableTypesRequest
        {
            NullableInt = null,
            NullableDouble = null,
            NullableBool = null,
            NullableDateTime = null,
            NullableGuid = null,
            NullableEnum = null,
            NullableString = null,
            QueryNullableInt = null
        };

        var (rsp, res, err) = await app.Client.POSTAsync<NullableTypesEndpoint, NullableTypesRequest, NullableTypesResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.NullCount.ShouldBe(8);
        res.NotNullCount.ShouldBe(0);
    }

    /// <summary>
    /// Tests array of complex objects binding in AOT mode.
    /// AOT ISSUE: Array/List element type instantiation uses reflection.
    /// JsonSerializer needs to know element type for deserialization.
    /// IList<T> population requires runtime generic collection creation.
    /// </summary>
    [Fact]
    public async Task Array_Object_Binding_Works_In_AOT_Mode()
    {
        var req = new ArrayValidationRequest
        {
            Items =
            [
                new() { Name = "Item1", Value = 10 },
                new() { Name = "Item2", Value = 20 },
                new() { Name = "Item3", Value = 30 }
            ],
            Numbers = [1, 2, 3, 4, 5],
            Strings = ["a", "b", "c"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ArrayValidationEndpoint, ArrayValidationRequest, ArrayValidationResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ItemCount.ShouldBe(3);
        res.NumberCount.ShouldBe(5);
        res.StringCount.ShouldBe(3);
        res.TotalSum.ShouldBe(60 + 15); // 10+20+30 + 1+2+3+4+5
        res.AllNames.ShouldBe("Item1, Item2, Item3");
    }

    /// <summary>
    /// Tests polymorphic JSON serialization (dog subtype) in AOT mode.
    /// AOT ISSUE: [JsonDerivedType] requires all derived types in JsonSerializerContext.
    /// Runtime type discrimination ($type) needs reflection to resolve actual type.
    /// </summary>
    [Fact]
    public async Task Polymorphic_Types_Dog_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<PolymorphicEndpoint, PolymorphicRequest, DogResponse>(
            new() { AnimalType = "dog", Name = "Buddy", Age = 5 });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("Buddy");
        res.Age.ShouldBe(5);
        res.Breed.ShouldBe("Labrador");
        res.CanFetch.ShouldBeTrue();
    }

    /// <summary>
    /// Tests polymorphic JSON serialization (cat subtype) in AOT mode.
    /// AOT ISSUE: Each derived type needs explicit registration for AOT serialization.
    /// Base type AnimalResponse must have all subtypes declared at compile time.
    /// </summary>
    [Fact]
    public async Task Polymorphic_Types_Cat_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<PolymorphicEndpoint, PolymorphicRequest, CatResponse>(
            new() { AnimalType = "cat", Name = "Whiskers", Age = 3 });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("Whiskers");
        res.Age.ShouldBe(3);
        res.IsIndoor.ShouldBeTrue();
        res.LivesRemaining.ShouldBe(9);
    }

    /// <summary>
    /// Tests Endpoint<TReq, Results<Ok<T>, NotFound, BadRequest>> union return type in AOT mode.
    /// AOT ISSUE: Results<> union type uses runtime type switching which needs reflection.
    /// Each Ok<T>/NotFound/BadRequest variant must have IResult implementation preserved.
    /// Type discrimination at runtime for Results<> doesn't work without reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Endpoint<TRequest, Results<>> return type doesn't work in Native AOT mode
    public async Task TypedResults_Ok_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<TypedResultsEndpoint, TypedResultsRequest, TypedResultsData>(
            new() { Scenario = 1 });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Message.ShouldBe("Success");
        res.Scenario.ShouldBe(1);
    }

    /// <summary>
    /// Tests Results<> returning NotFound variant in AOT mode.
    /// AOT ISSUE: TypedResults.NotFound() implementation must be available at runtime.
    /// Union type Results<> variant selection requires runtime type dispatch.
    /// </summary>
    [Fact]
    public async Task TypedResults_NotFound_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("typed-results-endpoint?scenario=2");
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests Results<> returning BadRequest variant in AOT mode.
    /// AOT ISSUE: Each IResult variant in Results<> needs explicit preservation.
    /// BadRequest response body may include error details requiring serialization.
    /// </summary>
    [Fact]
    public async Task TypedResults_BadRequest_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("typed-results-endpoint?scenario=3");
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests JWT token creation in AOT mode.
    /// AOT ISSUE: JWT creation uses System.Security.Claims which has reflection dependencies.
    /// Claim type resolution and signing algorithm selection may use reflection.
    /// </summary>
    [Fact]
    public async Task Jwt_Creation_Works_In_AOT_Mode()
    {
        var req = new JwtCreateRequest
        {
            UserId = "user-123",
            Username = "testuser",
            Roles = ["admin", "user"],
            ExpiryMinutes = 30
        };

        var (rsp, res, err) = await app.Client.POSTAsync<JwtCreationEndpoint, JwtCreateRequest, JwtCreateResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Token.ShouldNotBeNullOrEmpty();
        res.UserId.ShouldBe("user-123");
        res.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    /// <summary>
    /// Tests JWT-protected endpoint with [Authorize] in AOT mode.
    /// AOT ISSUE: Authorization policies use IAuthorizationHandler<T> resolved via reflection.
    /// Claim extraction from JWT token may use reflection for custom claim types.
    /// </summary>
    [Fact]
    public async Task Jwt_Protected_Endpoint_Works_In_AOT_Mode()
    {
        // First create a token
        var createReq = new JwtCreateRequest
        {
            UserId = "user-456",
            Username = "protecteduser",
            Roles = ["user"],
            ExpiryMinutes = 30
        };

        var (createRsp, createRes, createErr) = await app.Client.POSTAsync<JwtCreationEndpoint, JwtCreateRequest, JwtCreateResponse>(createReq);

        if (!createRsp.IsSuccessStatusCode)
            Assert.Fail(createErr);

        // Now use the token to access protected endpoint
        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", createRes.Token);

        try
        {
            var (rsp, res, err) = await app.Client.GETAsync<JwtProtectedEndpoint, JwtProtectedRequest, JwtProtectedResponse>(new());

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.UserId.ShouldBe("user-456");
            res.Username.ShouldBe("protecteduser");
            res.IsAuthenticated.ShouldBeTrue();
        }
        finally
        {
            app.Client.DefaultRequestHeaders.Authorization = null;
        }
    }

    /// <summary>
    /// Tests Routes() method for defining multiple routes in AOT mode.
    /// AOT ISSUE: Routes() returns string array processed at endpoint registration.
    /// Method invocation during startup uses reflection for endpoint discovery.
    /// </summary>
    [Fact]
    public async Task Routes_Method_Works_In_AOT_Mode()
    {
        // Test both routes defined via Routes() method
        var rspA = await app.Client.GetAsync("routes-method-a?source=routeA");
        rspA.IsSuccessStatusCode.ShouldBeTrue();

        var rspB = await app.Client.GetAsync("routes-method-b?source=routeB");
        rspB.IsSuccessStatusCode.ShouldBeTrue();
    }

    // ==================== MORE AOT ISSUE TESTS ====================

    /// <summary>
    /// Tests [BindFrom] attribute for property name aliasing in AOT mode.
    /// AOT ISSUE: [BindFrom("alias")] requires attribute reflection to discover alias names.
    /// Property-to-JSON-property mapping uses GetCustomAttribute<BindFromAttribute>().
    /// This binding source aliasing is entirely reflection-based and fails in AOT.
    /// </summary>
    [Fact] // AOT ISSUE: BindFrom attribute for property name aliasing may not work in Native AOT mode
    public async Task BindFrom_Attribute_Works_In_AOT_Mode()
    {
        var req = new BindFromRequest
        {
            CustomerId = 123,
            ProductName = "Test Product",
            Quantity = 5,
            Category = "electronics"
        };

        // Send with aliased JSON property names
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                customer_id = 123,
                product_name = "Test Product",
                qty = 5
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("bind-from-test?cat=electronics", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<BindFromResponse>();

        res.ShouldNotBeNull();
        res.CustomerId.ShouldBe(123);
        res.ProductName.ShouldBe("Test Product");
        res.Quantity.ShouldBe(5);
        res.Category.ShouldBe("electronics");
        res.AllBindingsWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Resolve<T>() service locator pattern in AOT mode.
    /// AOT ISSUE: Resolve<T>() uses IServiceProvider.GetService(typeof(T)) which is reflection-based.
    /// Runtime service type resolution cannot work without type metadata.
    /// Generic service resolution IService<T> requires MakeGenericType().
    /// </summary>
    [Fact] // AOT ISSUE: Resolve<T> service locator pattern may require reflection in Native AOT mode
    public async Task Service_Resolve_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<ServiceResolveEndpoint, ServiceResolveResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ResolveWorked.ShouldBeTrue();
        res.Message.ShouldBe("Service resolved successfully!");
        res.ServiceInstanceId.ShouldNotBe(Guid.Empty);
    }

    /// <summary>
    /// Tests TryResolve<T>() pattern in AOT mode.
    /// AOT ISSUE: TryResolve internally uses GetService with null checks.
    /// Service existence checking requires type metadata preservation.
    /// </summary>
    [Fact] // AOT ISSUE: TryResolve<T> pattern may require reflection in Native AOT mode
    public async Task TryResolve_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<TryResolveEndpoint, TryResolveResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ServiceFound.ShouldBeTrue();
        res.Message.ShouldBe("Service resolved successfully!");
    }

    /// <summary>
    /// Tests [HasPermission] attribute-based authorization in AOT mode.
    /// AOT ISSUE: Permission checking uses IAuthorizationHandler discovered via reflection.
    /// Custom permission attributes require runtime attribute scanning.
    /// Policy-based authorization resolves handlers dynamically.
    /// </summary>
    [Fact] // AOT ISSUE: HasPermission attribute binding may not work in Native AOT mode
    public async Task HasPermission_Attribute_Works_In_AOT_Mode()
    {
        // First get a JWT token with permissions
        var createReq = new JwtCreateRequest
        {
            UserId = "user-perm-test",
            Username = "permissionuser",
            Roles = ["admin"],
            ExpiryMinutes = 30
        };

        var (createRsp, createRes, createErr) = await app.Client.POSTAsync<JwtCreationEndpoint, JwtCreateRequest, JwtCreateResponse>(createReq);

        if (!createRsp.IsSuccessStatusCode)
            Assert.Fail(createErr);

        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", createRes.Token);

        try
        {
            var (rsp, res, err) = await app.Client.GETAsync<HasPermissionEndpoint, PermissionCheckRequest, PermissionCheckResponse>(new());

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            // The HasPermission attributes should have bound the permission checks
            res.UserId.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            app.Client.DefaultRequestHeaders.Authorization = null;
        }
    }

    /// <summary>
    /// Tests Server-Sent Events (SSE) with IAsyncEnumerable in AOT mode.
    /// AOT ISSUE: IAsyncEnumerable<T> streaming requires runtime async state machine.
    /// Event serialization for SSE needs type metadata for each yielded item.
    /// Async enumerable iteration uses reflection for MoveNextAsync/Current.
    /// </summary>
    [Fact] // AOT ISSUE: Server-Sent Events with IAsyncEnumerable may not work in Native AOT mode
    public async Task EventStream_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("event-stream-test?eventName=test&eventCount=3&delayMs=10");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var content = await rsp.Content.ReadAsStringAsync();

        // SSE format should contain event data
        content.ShouldContain("data:");
        content.ShouldContain("Event");
    }

    /// <summary>
    /// Tests Dictionary<K,V> binding with various key/value types in AOT mode.
    /// AOT ISSUE: Dictionary deserialization requires JsonConverter for key type.
    /// Dictionary<int, string> needs int-to-string key conversion via reflection.
    /// Nested Dictionary<string, List<T>> requires multiple generic instantiations.
    /// </summary>
    [Fact] // AOT ISSUE: Dictionary binding with various key/value types may not work in Native AOT mode
    public async Task Dictionary_Binding_Works_In_AOT_Mode()
    {
        var req = new DictionaryBindingRequest
        {
            StringDict = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" },
            IntDict = new Dictionary<string, int> { ["count"] = 10, ["total"] = 100 },
            IntKeyDict = new Dictionary<int, string> { [1] = "one", [2] = "two" },
            ListValueDict = new Dictionary<string, List<string>> { ["tags"] = ["a", "b", "c"] }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DictionaryBindingEndpoint, DictionaryBindingRequest, DictionaryBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.StringDictCount.ShouldBe(2);
        res.IntDictCount.ShouldBe(2);
        res.IntKeyDictCount.ShouldBe(2);
        res.ListValueDictCount.ShouldBe(1);
        res.AllDictionariesBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests C# record types as request DTOs in AOT mode.
    /// AOT ISSUE: Records use compiler-generated Equals/GetHashCode with reflection.
    /// Record primary constructor binding may use reflection for parameter matching.
    /// Positional records (Name, Age, ...) need constructor parameter discovery.
    /// </summary>
    [Fact] // AOT ISSUE: Record types as request DTOs may not work in Native AOT mode
    public async Task Record_Types_Work_In_AOT_Mode()
    {
        var req = new RecordRequest(
            Name: "John Doe",
            Age: 30,
            IsActive: true,
            CreatedAt: DateTime.UtcNow
        );

        var (rsp, res, err) = await app.Client.POSTAsync<RecordTypesEndpoint, RecordRequest, RecordResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("John Doe");
        res.Age.ShouldBe(30);
        res.IsActive.ShouldBeTrue();
        res.ProcessedMessage.ShouldContain("John Doe");
    }

    /// <summary>
    /// Tests record with init-only properties in AOT mode.
    /// AOT ISSUE: Init-only setters (init;) require special IL for initialization.
    /// JsonSerializer uses reflection to call init setters during deserialization.
    /// Property.SetValue() on init properties needs runtime metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Record with init properties may not work in Native AOT mode
    public async Task Init_Property_Record_Works_In_AOT_Mode()
    {
        var req = new InitPropertyRecord
        {
            Id = "test-id",
            Value = "test-value",
            Number = 42
        };

        var (rsp, res, err) = await app.Client.POSTAsync<InitPropertyRecordEndpoint, InitPropertyRecord, InitPropertyRecord>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Id.ShouldBe("test-id");
        res.Value.ShouldContain("Modified");
        res.Number.ShouldBe(42);
    }

    /// <summary>
    /// Tests struct types as request DTOs in AOT mode.
    /// AOT ISSUE: Struct binding requires boxing/unboxing which needs type metadata.
    /// Value type instantiation differs from reference types in AOT.
    /// Struct default values and parameterless constructor handling use reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Struct types as request DTOs may not work in Native AOT mode
    public async Task Struct_Types_Work_In_AOT_Mode()
    {
        var req = new StructRequest
        {
            Id = 123,
            Name = "Test Struct",
            Value = 99.99
        };

        var (rsp, res, err) = await app.Client.POSTAsync<StructTypesEndpoint, StructRequest, StructResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Id.ShouldBe(123);
        res.Name.ShouldBe("Test Struct");
        res.Value.ShouldBe(99.99);
        res.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// Tests default property values in request DTOs in AOT mode.
    /// AOT ISSUE: Default values like 'public int Count { get; set; } = 10' need metadata.
    /// Field initializers are compiled to constructor code which may be trimmed.
    /// Default value preservation requires DTO constructor preservation.
    /// </summary>
    [Fact] // AOT ISSUE: Default values in request DTOs may not be preserved in Native AOT mode
    public async Task Default_Values_Work_In_AOT_Mode()
    {
        // Send empty JSON to test default values
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var rsp = await app.Client.PostAsync("default-values-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<DefaultValuesResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("DefaultName");
        res.Count.ShouldBe(10);
        res.IsEnabled.ShouldBeTrue();
        res.Rate.ShouldBe(0.5);
        res.DefaultsPreserved.ShouldBeTrue();
    }

    /// <summary>
    /// Tests inherited request DTOs (ChildRequest : BaseRequest) in AOT mode.
    /// AOT ISSUE: Inheritance hierarchy requires all base type properties preserved.
    /// GetProperties(BindingFlags.Instance | BindingFlags.Public) includes inherited props.
    /// Polymorphic binding needs runtime type checking for actual derived type.
    /// </summary>
    [Fact] // AOT ISSUE: Inherited request DTOs may not work in Native AOT mode
    public async Task Inherited_DTO_Works_In_AOT_Mode()
    {
        var req = new InheritedRequest
        {
            BaseProperty = "base-value",
            BaseId = 100,
            ChildProperty = "child-value",
            ChildFlag = true
        };

        var (rsp, res, err) = await app.Client.POSTAsync<InheritedDtoEndpoint, InheritedRequest, InheritedResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.BaseMessage.ShouldContain("base-value");
        res.ChildMessage.ShouldContain("child-value");
        res.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests generic base endpoint classes in AOT mode.
    /// AOT ISSUE: class MyEndpoint : GenericBase<T> requires generic instantiation.
    /// Base class virtual methods with generics need runtime dispatch.
    /// MakeGenericType() for endpoint base classes fails in AOT.
    /// </summary>
    [Fact] // AOT ISSUE: Generic base endpoint classes may not work in Native AOT mode
    public async Task Generic_Inherited_Endpoint_Works_In_AOT_Mode()
    {
        var req = new GenericInheritedRequest { Data = "test-data" };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericInheritedEndpoint, GenericInheritedRequest, GenericInheritedResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Data.ShouldBe("test-data");
        res.RequestTypeName.ShouldBe("GenericInheritedRequest");
    }

    // ==================== ROUND 3: MORE AOT TESTS ====================

    /// <summary>
    /// Tests [FromBody] explicit body binding attribute in AOT mode.
    /// AOT ISSUE: [FromBody] attribute discovery uses GetCustomAttribute reflection.
    /// Explicit body binding bypasses normal property-by-property binding.
    /// Nested object in [FromBody] property needs complete type metadata.
    /// </summary>
    [Fact] // AOT ISSUE: [FromBody] attribute for explicit body binding may not work in Native AOT mode
    public async Task FromBody_Attribute_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = "Test Body",
                Value = 42,
                Tags = new[] { "tag1", "tag2" }
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("from-body-test?QueryValue=query-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<FromBodyResponse>();

        res.ShouldNotBeNull();
        res.QueryValue.ShouldBe("query-test");
        res.BodyName.ShouldBe("Test Body");
        res.BodyValue.ShouldBe(42);
        res.TagCount.ShouldBe(2);
        res.BodyWasBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests custom value types with IParsable<T> implementation in AOT mode.
    /// AOT ISSUE: IParsable<T>.Parse() is discovered via reflection interface scanning.
    /// Custom types like CustomId need Parse method preserved and callable.
    /// typeof(T).GetMethod("Parse") reflection fails in trimmed AOT.
    /// </summary>
    [Fact] // AOT ISSUE: Custom value types with IParsable<T> may not work in Native AOT mode
    public async Task Custom_Value_Parser_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<CustomValueParserEndpoint, CustomValueParserRequest, CustomValueParserResponse>(
            new() { Id = new CustomId("PRD", 123), OptionalId = new CustomId("OPT", 456) });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IdPrefix.ShouldBe("PRD");
        res.IdNumber.ShouldBe(123);
        res.OptionalIdPrefix.ShouldBe("OPT");
        res.OptionalIdNumber.ShouldBe(456);
        res.CustomParserWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests deeply nested objects (4+ levels) binding in AOT mode.
    /// AOT ISSUE: Each nesting level requires recursive property discovery.
    /// Deep object graphs Level1.Level2.Level3.Level4 need all intermediate types.
    /// Memory allocation for nested objects uses Activator.CreateInstance per level.
    /// </summary>
    [Fact] // AOT ISSUE: Deeply nested objects (4+ levels) may not work in Native AOT mode
    public async Task Deep_Nested_Objects_Work_In_AOT_Mode()
    {
        var req = new DeepNestedRequest
        {
            RootValue = "root",
            Level1 = new Level1
            {
                Value1 = "level1",
                Level2 = new Level2
                {
                    Value2 = "level2",
                    Level3 = new Level3
                    {
                        Value3 = "level3",
                        Level4 = new Level4
                        {
                            DeepValue = "deep",
                            DeepNumber = 999
                        }
                    }
                }
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DeepNestedEndpoint, DeepNestedRequest, DeepNestedResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.RootValue.ShouldBe("root");
        res.Level1Value.ShouldBe("level1");
        res.Level2Value.ShouldBe("level2");
        res.Level3Value.ShouldBe("level3");
        res.Level4Value.ShouldBe("deep");
        res.Level4Number.ShouldBe(999);
        res.AllLevelsBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests interface-based DTOs (property typed as IDataContainer) in AOT mode.
    /// AOT ISSUE: Interface properties require concrete type resolution at deserialization.
    /// JSON cannot instantiate IDataContainer - needs $type discriminator or explicit type.
    /// Polymorphic interface binding uses reflection to find implementing types.
    /// </summary>
    [Fact] // AOT ISSUE: Interface-based DTOs may not work in Native AOT mode
    public async Task Interface_Dto_Works_In_AOT_Mode()
    {
        var req = new InterfaceDtoRequest
        {
            Name = "Test Interface",
            Container = new ConcreteDataContainer
            {
                Id = "container-123",
                Data = "container-data",
                ExtraInfo = "extra-info"
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<InterfaceDtoEndpoint, InterfaceDtoRequest, InterfaceDtoResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("Test Interface");
        res.ContainerId.ShouldBe("container-123");
        res.ContainerData.ShouldBe("container-data");
        res.ExtraInfo.ShouldBe("extra-info");
        res.InterfaceWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests array binding from query string (Ids=1&Ids=2&Ids=3) in AOT mode.
    /// AOT ISSUE: Query collection to array conversion uses runtime array creation.
    /// Array.CreateInstance(elementType, count) requires type metadata.
    /// Multiple query values with same key need collection instantiation.
    /// </summary>
    [Fact] // AOT ISSUE: Array query binding may not work in Native AOT mode
    public async Task Array_Query_Binding_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("array-query-binding-test?Ids=1&Ids=2&Ids=3&Names=Alice&Names=Bob&Numbers=10&Numbers=20");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<ArrayQueryBindingResponse>();

        res.ShouldNotBeNull();
        res.IdCount.ShouldBe(3);
        res.NameCount.ShouldBe(2);
        res.NumberCount.ShouldBe(2);
        res.FirstId.ShouldBe(1);
        res.FirstName.ShouldBe("Alice");
        res.ArraysBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Send.BytesAsync() file response in AOT mode.
    /// AOT ISSUE: File content-type detection may use MIME type reflection.
    /// Response stream handling needs preserved IO implementations.
    /// </summary>
    [Fact] // AOT ISSUE: Send.BytesAsync may not work in Native AOT mode
    public async Task Send_File_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("send-file-test?FileName=test.txt&Content=HelloAOT&ContentType=text/plain");

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        
        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldBe("HelloAOT");
        
        rsp.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
    }

    /// <summary>
    /// Tests Send.StreamAsync() response in AOT mode.
    /// AOT ISSUE: Stream copying uses async methods that may be trimmed.
    /// Content-length calculation and chunked transfer need runtime support.
    /// </summary>
    [Fact] // AOT ISSUE: Send.StreamAsync may not work in Native AOT mode
    public async Task Send_Stream_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("send-stream-test?Data=StreamContent");

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        
        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldBe("StreamContent");
    }

    /// <summary>
    /// Tests endpoint with multiple HTTP verbs (GET+POST) - GET request in AOT mode.
    /// AOT ISSUE: Multiple verbs require separate route registrations.
    /// Verb detection uses HttpMethodAttribute scanning via reflection.
    /// Same endpoint handling different verbs needs method dispatch.
    /// </summary>
    [Fact] // AOT ISSUE: Multiple HTTP verbs on single endpoint may not work in Native AOT mode
    public async Task Multiple_Verbs_GET_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("multiple-verbs-test?QueryData=get-query");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<MultipleVerbsResponse>();

        res.ShouldNotBeNull();
        res.HttpMethod.ShouldBe("GET");
        res.QueryData.ShouldBe("get-query");
        res.IsGet.ShouldBeTrue();
    }

    /// <summary>
    /// Tests endpoint with multiple HTTP verbs (GET+POST) - POST request in AOT mode.
    /// AOT ISSUE: Same endpoint handling POST needs body deserialization.
    /// Verb-specific binding (query for GET, body for POST) uses runtime dispatch.
    /// </summary>
    [Fact] // AOT ISSUE: Multiple HTTP verbs on single endpoint may not work in Native AOT mode
    public async Task Multiple_Verbs_POST_Works_In_AOT_Mode()
    {
        var req = new MultipleVerbsRequest { Data = "post-data", QueryData = "post-query" };

        var (rsp, res, err) = await app.Client.POSTAsync<MultipleVerbsEndpoint, MultipleVerbsRequest, MultipleVerbsResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HttpMethod.ShouldBe("POST");
        res.Data.ShouldBe("post-data");
        res.IsPost.ShouldBeTrue();
    }

    /// <summary>
    /// Tests property injection on endpoints in AOT mode.
    /// AOT ISSUE: Property injection uses reflection to set property values.
    /// PropertyInfo.SetValue() for [Inject] marked properties needs preservation.
    /// Unlike constructor injection, property injection requires runtime invocation.
    /// </summary>
    [Fact] // AOT ISSUE: Property injection may not work in Native AOT mode
    public async Task Property_Injection_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<PropertyInjectionEndpoint, PropertyInjectionResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.InjectedValue.ShouldBe("Property Injected!");
        res.InjectedServiceId.ShouldNotBe(Guid.Empty);
        res.PropertyInjectionWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests constructor injection on endpoints in AOT mode.
    /// AOT ISSUE: Constructor parameter resolution uses DI container reflection.
    /// GetConstructor() and Invoke() for dependency injection need preserved metadata.
    /// Multiple constructor parameters require full constructor info.
    /// </summary>
    [Fact] // AOT ISSUE: Constructor injection may not work in Native AOT mode
    public async Task Constructor_Injection_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<ConstructorInjectionEndpoint, ConstructorInjectionResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.InjectedValue.ShouldBe("Property Injected!");
        res.InjectedServiceId.ShouldNotBe(Guid.Empty);
        res.ConstructorInjectionWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests DateTime/DateOnly/TimeOnly binding in AOT mode.
    /// AOT ISSUE: DateTime parsing uses culture-specific format providers.
    /// TypeConverter for DateTime types uses reflection-based converter lookup.
    /// ISO 8601 parsing needs DateTimeStyles enum handling.
    /// </summary>
    [Fact] // AOT ISSUE: DateTime/DateOnly/TimeOnly binding may not work in Native AOT mode
    public async Task DateTime_Binding_Works_In_AOT_Mode()
    {
        var testDate = new DateTime(2025, 1, 31, 14, 30, 0, DateTimeKind.Utc);
        var testDateOnly = new DateOnly(2025, 1, 31);
        var testTimeOnly = new TimeOnly(14, 30, 0);
        var testTimeSpan = TimeSpan.FromHours(2.5);
        
        var req = new DateTimeBindingRequest
        {
            DateTime = testDate,
            DateTimeOffset = new DateTimeOffset(testDate),
            DateOnly = testDateOnly,
            TimeOnly = testTimeOnly,
            TimeSpan = testTimeSpan
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DateTimeBindingEndpoint, DateTimeBindingRequest, DateTimeBindingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Year.ShouldBe(2025);
        res.Month.ShouldBe(1);
        res.Day.ShouldBe(31);
        res.AllDateTimesBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests ThrowError/AddError in AOT mode.
    /// AOT ISSUE: Error response serialization requires preserved error DTO types.
    /// Exception handling with custom properties uses reflection for serialization.
    /// ValidationFailure collection needs type metadata.
    /// </summary>
    [Fact] // AOT ISSUE: ThrowError/AddError may not work correctly in Native AOT mode
    public async Task ThrowError_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("error-details-test?ShouldSucceed=false&ErrorMessage=TestError");

        // Should return 400 Bad Request with error
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests validation error serialization in AOT mode.
    /// AOT ISSUE: FluentValidation error messages use expression trees.
    /// Property name extraction from validation failure needs reflection.
    /// Error dictionary serialization requires preserved types.
    /// </summary>
    [Fact] // AOT ISSUE: Validation errors may not serialize correctly in Native AOT mode
    public async Task Validation_Error_Works_In_AOT_Mode()
    {
        var req = new ValidationErrorRequest
        {
            RequiredField = "",  // Empty - should fail
            MustBePositive = -1  // Negative - should fail
        };

        var (rsp, _, _) = await app.Client.POSTAsync<ValidationErrorEndpoint, ValidationErrorRequest, ErrorDetailsSuccessResponse>(req);

        // Should return 400 Bad Request with validation errors
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    // ==================== ROUND 4: MORE AOT TESTS ====================

    /// <summary>
    /// Tests multiple route parameters in AOT mode.
    /// AOT ISSUE: Route parameter binding uses RouteValueDictionary reflection.
    /// Parameter name matching to DTO properties uses PropertyInfo discovery.
    /// Multiple segments /{category}/{productId}/{variant} need ordered binding.
    /// </summary>
    [Fact] // AOT ISSUE: Multiple route parameters may not bind correctly in Native AOT mode
    public async Task Multiple_Route_Params_Work_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("category/electronics/product/123/variant/blue?Filter=instock");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<MultipleRouteParamsResponse>();

        res.ShouldNotBeNull();
        res.Category.ShouldBe("electronics");
        res.ProductId.ShouldBe(123);
        res.Variant.ShouldBe("blue");
        res.Filter.ShouldBe("instock");
        res.AllParamsBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Guid binding from route, query, and body in AOT mode.
    /// AOT ISSUE: Guid.Parse() is called via reflection for string-to-Guid conversion.
    /// Collection of Guids (List<Guid>) needs preserved JsonConverter.
    /// Dictionary<string, Guid> requires key/value type metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Guid binding in route and body may not work in Native AOT mode
    public async Task Guid_Binding_Works_In_AOT_Mode()
    {
        var testGuid = Guid.NewGuid();
        var queryGuid = Guid.NewGuid();
        var listGuid1 = Guid.NewGuid();
        var listGuid2 = Guid.NewGuid();

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                GuidList = new[] { listGuid1, listGuid2 },
                GuidDict = new Dictionary<string, Guid> { ["key1"] = Guid.NewGuid() }
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync($"guid-binding/{testGuid}?QueryGuid={queryGuid}", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<GuidBindingResponse>();

        res.ShouldNotBeNull();
        res.Id.ShouldBe(testGuid);
        res.QueryGuid.ShouldBe(queryGuid);
        res.GuidListCount.ShouldBe(2);
        res.GuidDictCount.ShouldBe(1);
        res.GuidsBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Send.CreatedAtAsync() with location header in AOT mode.
    /// AOT ISSUE: CreatedAt generates location URL using endpoint discovery.
    /// LinkGenerator.GetPathByName() uses route reflection.
    /// Response with Created status needs preserved response type.
    /// </summary>
    [Fact] // AOT ISSUE: Send.CreatedAtAsync may not work in Native AOT mode
    public async Task CreatedAt_Response_Works_In_AOT_Mode()
    {
        var req = new CreatedAtRequest
        {
            Name = "Test Resource",
            Value = "Test Value"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<CreatedAtEndpoint, CreatedAtRequest, CreatedAtResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        res.Name.ShouldBe("Test Resource");
        res.Value.ShouldBe("Test Value");
        res.Id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>
    /// Tests various numeric types (decimal, float, long, etc.) in AOT mode.
    /// AOT ISSUE: Numeric type converters use reflection for type dispatch.
    /// TypeDescriptor.GetConverter() for decimal/float may be trimmed.
    /// Unsigned types (uint, ulong) need specific converter metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Numeric types (decimal, float, etc.) may not bind correctly in Native AOT mode
    public async Task Numeric_Types_Binding_Works_In_AOT_Mode()
    {
        var req = new NumericTypesRequest
        {
            DecimalValue = 123.456m,
            FloatValue = 78.9f,
            DoubleValue = 456.789,
            LongValue = 9876543210L,
            ShortValue = 32000,
            ByteValue = 255,
            UIntValue = 4000000000,
            ULongValue = 18000000000000000000
        };

        var (rsp, res, err) = await app.Client.POSTAsync<NumericTypesEndpoint, NumericTypesRequest, NumericTypesResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.DecimalValue.ShouldBe(123.456m);
        res.LongValue.ShouldBe(9876543210L);
        res.ByteValue.ShouldBe((byte)255);
        res.AllNumericsBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests HttpContext property access in endpoint in AOT mode.
    /// AOT ISSUE: HttpContext properties may use reflection for feature access.
    /// Features.Get<IFeature>() uses interface reflection lookup.
    /// Request properties like Path, Method come from HttpRequest reflection.
    /// </summary>
    [Fact] // AOT ISSUE: HttpContext access may have issues in Native AOT mode
    public async Task HttpContext_Access_Works_In_AOT_Mode()
    {
        var (rsp, res, err) = await app.Client.GETAsync<HttpContextAccessEndpoint, HttpContextAccessResponse>();

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.RequestPath.ShouldBe("/http-context-access-test");
        res.RequestMethod.ShouldBe("GET");
        res.Scheme.ShouldNotBeNullOrEmpty();
        res.HttpContextAccessible.ShouldBeTrue();
    }

    /// <summary>
    /// Tests query-only parameter binding in AOT mode.
    /// AOT ISSUE: Query string collection binding uses QueryHelpers reflection.
    /// Multiple query params need property discovery and type conversion.
    /// Pagination parameters require arithmetic in derived properties.
    /// </summary>
    [Fact] // AOT ISSUE: Query-only parameters may not bind correctly in Native AOT mode
    public async Task Query_Only_Params_Work_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("query-only-test?Search=test&Page=2&PageSize=25&SortBy=name&Ascending=false");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<QueryOnlyResponse>();

        res.ShouldNotBeNull();
        res.Search.ShouldBe("test");
        res.Page.ShouldBe(2);
        res.PageSize.ShouldBe(25);
        res.SortBy.ShouldBe("name");
        res.Ascending.ShouldBeFalse();
        res.Skip.ShouldBe(25); // (2-1) * 25
    }

    /// <summary>
    /// Tests boolean binding from query and body in AOT mode.
    /// AOT ISSUE: Boolean parsing from string ("true"/"false") uses TryParse.
    /// Boolean arrays List<bool> need preserved collection type.
    /// Default bool values require field initializer preservation.
    /// </summary>
    [Fact] // AOT ISSUE: Boolean binding from query and body may not work in Native AOT mode
    public async Task Boolean_Binding_Works_In_AOT_Mode()
    {
        var req = new BooleanBindingRequest
        {
            BoolFromJson = true,
            BoolList = [true, false, true, true]
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                BoolFromJson = true,
                BoolList = new[] { true, false, true, true },
                DefaultTrue = false,
                DefaultFalse = true
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("boolean-binding-test?QueryBool=true&NullableQueryBool=false", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<BooleanBindingResponse>();

        res.ShouldNotBeNull();
        res.BoolFromJson.ShouldBeTrue();
        res.QueryBool.ShouldBeTrue();
        res.NullableQueryBool.ShouldBe(false);
        res.BoolListCount.ShouldBe(4);
        res.TrueCount.ShouldBe(3);
        res.BooleansBound.ShouldBeTrue();
    }

    // =====================================================
    // ROUND 5: ADDITIONAL AOT TESTS
    // =====================================================

    /// <summary>
    /// Tests polymorphic JSON serialization with JsonDerivedType in AOT mode.
    /// AOT ISSUE: [JsonDerivedType] requires runtime type discriminator handling.
    /// Polymorphic serialization uses type metadata to select derived type.
    /// $type field handling needs reflection for type resolution.
    /// </summary>
    [Fact] // AOT ISSUE: Polymorphic serialization with JsonDerivedType may not work in Native AOT mode
    public async Task Polymorphic_Response_Works_In_AOT_Mode()
    {
        // Test dog response
        var rspDog = await app.Client.GetAsync("polymorphic-response?AnimalType=dog");
        
        if (!rspDog.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rspDog.Content.ReadAsStringAsync()}");

        var dogJson = await rspDog.Content.ReadAsStringAsync();
        dogJson.ShouldContain("Buddy");
        dogJson.ShouldContain("Woof");

        // Test cat response
        var rspCat = await app.Client.GetAsync("polymorphic-response?AnimalType=cat");
        
        if (!rspCat.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rspCat.Content.ReadAsStringAsync()}");

        var catJson = await rspCat.Content.ReadAsStringAsync();
        catJson.ShouldContain("Whiskers");
        catJson.ShouldContain("Meow");
    }

    /// <summary>
    /// Tests complex dictionary binding with nested types in AOT mode.
    /// AOT ISSUE: Dictionary<K, List<V>> requires multiple generic instantiations.
    /// Dictionary<int, string> needs int key conversion.
    /// Nested types in dictionary values need recursive metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Complex dictionary binding with nested types may not work in Native AOT mode
    public async Task Dictionary_Complex_Binding_Works_In_AOT_Mode()
    {
        var req = new DictionaryComplexRequest
        {
            TagScores = new Dictionary<string, List<int>>
            {
                { "tag1", new List<int> { 1, 2, 3 } },
                { "tag2", new List<int> { 4, 5 } }
            },
            IdNames = new Dictionary<int, string>
            {
                { 1, "First" },
                { 2, "Second" }
            },
            Metadata = new Dictionary<string, NestedDictValue>
            {
                { "key1", new NestedDictValue { Value = "val1", Priority = 1 } }
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DictionaryComplexBindingEndpoint, DictionaryComplexRequest, DictionaryComplexResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.TagScoresCount.ShouldBe(2);
        res.IdNamesCount.ShouldBe(2);
        res.MetadataCount.ShouldBe(1);
        res.AllKeys.ShouldContain("tag1");
        res.AllKeys.ShouldContain("key1");
    }

    /// <summary>
    /// Tests tuple response serialization in AOT mode.
    /// AOT ISSUE: ValueTuple<T1, T2, T3> serialization needs preserved tuple metadata.
    /// Tuple item names (Item1, Item2) may not be preserved in AOT.
    /// Named tuple properties use compiler-generated attributes.
    /// </summary>
    [Fact] // AOT ISSUE: Tuple response serialization may not work in Native AOT mode
    public async Task Tuple_Response_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("tuple-response?a=10&b=3");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var json = await rsp.Content.ReadAsStringAsync();
        // Tuple should serialize with Item1, Item2, Item3 or Sum, Product, Difference
        (json.Contains("13") || json.Contains("Sum")).ShouldBeTrue(); // 10 + 3
    }

    /// <summary>
    /// Tests record types with init-only setters in AOT mode.
    /// AOT ISSUE: Init-only setters use special init accessor IL.
    /// Record deserialization calls init setters via reflection.
    /// Required init properties enforce initialization constraints.
    /// </summary>
    [Fact] // AOT ISSUE: Record types with init-only setters may not work in Native AOT mode
    public async Task Record_With_Init_Works_In_AOT_Mode()
    {
        var req = new RecordWithInitRequest
        {
            Name = "TestUser",
            Age = 30,
            OptionalField = "optional"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<RecordWithInitEndpoint, RecordWithInitRequest, RecordWithInitResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Greeting.ShouldContain("TestUser");
        res.Greeting.ShouldContain("30");
        res.HasOptional.ShouldBeTrue();
    }

    /// <summary>
    /// Tests IAsyncEnumerable streaming response in AOT mode.
    /// AOT ISSUE: IAsyncEnumerable<T> uses async state machine.
    /// Async iterator yield return generates runtime state machine code.
    /// Streaming JSON array serialization needs enumerable metadata.
    /// </summary>
    [Fact] // AOT ISSUE: IAsyncEnumerable streaming may not work in Native AOT mode
    public async Task Streaming_Response_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("streaming-response");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldContain("Item 0");
        content.ShouldContain("Item 4");
    }

    /// <summary>
    /// Tests JsonIgnore and other serialization attributes in AOT mode.
    /// AOT ISSUE: [JsonIgnore] discovery uses GetCustomAttribute reflection.
    /// [JsonPropertyName] attribute scanning may be trimmed.
    /// Conditional serialization based on attributes needs metadata.
    /// </summary>
    [Fact] // AOT ISSUE: JsonIgnore and other serialization attributes may not work in Native AOT mode
    public async Task Json_Ignore_Attribute_Works_In_AOT_Mode()
    {
        var req = new JsonIgnoreRequest
        {
            Username = "testuser",
            Password = "secret123", // Should be ignored
            Email = "test@example.com"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<JsonIgnoreEndpoint, JsonIgnoreRequest, JsonIgnoreResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Username.ShouldBe("testuser");
        res.DisplayName.ShouldBe("TESTUSER");
        res.Score.ShouldBe(100);

        // Verify InternalId is not in the raw JSON (should be ignored)
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(req),
            System.Text.Encoding.UTF8,
            "application/json");
        var rawRsp = await app.Client.PostAsync("json-ignore-test", content);
        var rawJson = await rawRsp.Content.ReadAsStringAsync();
        rawJson.ShouldNotContain("internalId");
    }

    /// <summary>
    /// Tests DateTime/DateOnly/TimeOnly types binding in AOT mode.
    /// AOT ISSUE: DateTime format parsing uses culture-specific IFormatProvider.
    /// TypeDescriptor.GetConverter for date types may be trimmed.
    /// TimeSpan parsing requires format string interpretation.
    /// </summary>
    [Fact] // AOT ISSUE: DateTime/DateOnly/TimeOnly binding may not work in Native AOT mode
    public async Task DateTime_Types_Binding_Works_In_AOT_Mode()
    {
        var now = DateTime.UtcNow;
        var req = new DateTimeTypesRequest
        {
            DateTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            DateTimeOffset = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero),
            DateOnly = new DateOnly(2025, 6, 15),
            TimeOnly = new TimeOnly(10, 30, 0),
            TimeSpan = TimeSpan.FromHours(2.5)
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DateTimeTypesEndpoint, DateTimeTypesRequest, DateTimeTypesResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.DateOnly.ShouldBe(new DateOnly(2025, 6, 15));
        res.TimeOnly.ShouldBe(new TimeOnly(10, 30, 0));
        res.DateOnlyFormatted.ShouldBe("2025-06-15");
    }

    /// <summary>
    /// Tests multi-level nested objects with dictionaries in AOT mode.
    /// AOT ISSUE: Deep nesting (4+ levels) with dictionaries requires recursive metadata.
    /// Dictionary<string, NestedType> values need full nested type info.
    /// List<NestedType> at each level needs collection + element metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Multi-level nested objects with dictionaries may not work in Native AOT mode
    public async Task Deep_Nesting_With_Dict_Works_In_AOT_Mode()
    {
        var req = new DeepNestingRequest
        {
            Root = new NestLevel1
            {
                TopValue = "top",
                Level2 = new NestLevel2
                {
                    MidValue = "mid",
                    Level3 = new NestLevel3
                    {
                        DeepValue = "deep1",
                        DeepNumber = 100
                    },
                    Level3List = new List<NestLevel3>
                    {
                        new() { DeepValue = "deep2", DeepNumber = 200 },
                        new() { DeepValue = "deep3", DeepNumber = 300 }
                    }
                },
                Level2Dict = new Dictionary<string, NestLevel2>
                {
                    { "dictKey", new NestLevel2 { MidValue = "dictMid", Level3 = new NestLevel3 { DeepValue = "dictDeep", DeepNumber = 400 } } }
                }
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DeepNestingEndpoint, DeepNestingRequest, DeepNestingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.DeepestValue.ShouldBe("deep1");
        res.TotalLevel3Count.ShouldBeGreaterThan(3);
        res.AllDeepValues.ShouldContain("deep1");
        res.AllDeepValues.ShouldContain("dictDeep");
    }

    /// <summary>
    /// Tests interface-typed properties (IList, IEnumerable, IDictionary) in AOT mode.
    /// AOT ISSUE: Interface deserialization requires concrete type instantiation.
    /// IList<T> creates List<T>, IDictionary creates Dictionary<K,V> at runtime.
    /// Type covariance/contravariance checking uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Interface properties in request may not deserialize correctly in Native AOT mode
    public async Task Interface_Property_Binding_Works_In_AOT_Mode()
    {
        var req = new InterfacePropertyRequest
        {
            Name = "TestName",
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Numbers = new[] { 1, 2, 3, 4, 5 },
            Properties = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<InterfacePropertyEndpoint, InterfacePropertyRequest, InterfacePropertyResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("TestName");
        res.TagCount.ShouldBe(3);
        res.NumberSum.ShouldBe(15);
        res.PropertyCount.ShouldBe(2);
    }

    // =====================================================
    // ROUND 7: ADDITIONAL AOT TESTS
    // =====================================================

    /// <summary>
    /// Tests Throttle() rate limiting configuration in AOT mode.
    /// AOT ISSUE: Throttle uses header-based client identification.
    /// ConcurrentDictionary for rate limit tracking uses runtime type handling.
    /// Header value extraction may use reflection for custom header names.
    /// </summary>
    [Fact] // AOT ISSUE: Throttle rate limiting may not work in Native AOT mode
    public async Task Throttle_Rate_Limiting_Works_In_AOT_Mode()
    {
        var req = new ThrottleRequest
        {
            ClientId = "test-client-123",
            RequestNumber = 1
        };

        // Add the throttle header
        app.Client.DefaultRequestHeaders.Add("X-Client-Id", "test-client-123");

        try
        {
            var (rsp, res, err) = await app.Client.POSTAsync<ThrottleTestEndpoint, ThrottleRequest, ThrottleResponse>(req);

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.ClientId.ShouldBe("test-client-123");
            res.ThrottleConfigured.ShouldBeTrue();
        }
        finally
        {
            app.Client.DefaultRequestHeaders.Remove("X-Client-Id");
        }
    }

    /// <summary>
    /// Tests Idempotency() configuration in AOT mode.
    /// AOT ISSUE: Idempotency uses header-based request identification.
    /// Cached response storage/retrieval uses reflection for serialization.
    /// IdempotencyOptions configuration involves reflection-based property setting.
    /// </summary>
    [Fact] // AOT ISSUE: Idempotency configuration may not work in Native AOT mode
    public async Task Idempotency_Works_In_AOT_Mode()
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        var req = new IdempotencyRequest
        {
            OperationId = "op-123",
            Data = "test-data"
        };

        // Add the idempotency header
        app.Client.DefaultRequestHeaders.Add("X-Idempotency-Key", idempotencyKey);

        try
        {
            var (rsp, res, err) = await app.Client.POSTAsync<IdempotencyTestEndpoint, IdempotencyRequest, IdempotencyResponse>(req);

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.OperationId.ShouldBe("op-123");
            res.IdempotencyConfigured.ShouldBeTrue();
        }
        finally
        {
            app.Client.DefaultRequestHeaders.Remove("X-Idempotency-Key");
        }
    }

    /// <summary>
    /// Tests expression-based route patterns in AOT mode.
    /// AOT ISSUE: Expression<Func<TRequest, object>> uses expression tree compilation.
    /// Route parameter extraction from lambda uses reflection.
    /// BuildRoute() method compiles expression at runtime.
    /// </summary>
    [Fact] // AOT ISSUE: Expression-based route patterns may not work in Native AOT mode
    public async Task Expression_Route_Pattern_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("order/123/product/ABC-456?Quantity=10");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<ExpressionRouteResponse>();

        res.ShouldNotBeNull();
        res.OrderId.ShouldBe(123);
        res.ProductCode.ShouldBe("ABC-456");
        res.ExpressionRouteWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests custom BindAsync static method in AOT mode.
    /// AOT ISSUE: BindAsync discovery uses reflection to find static methods.
    /// typeof(T).GetMethod("BindAsync") fails in trimmed AOT.
    /// Method invocation uses MethodInfo.Invoke which is reflection-based.
    /// </summary>
    [Fact] // AOT ISSUE: Custom BindAsync static method may not work in Native AOT mode
    public async Task Custom_BindAsync_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("custom-binding-test?Name=TestName&customId=PRD-123&tryParseValue=hello");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<CustomBindingResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("TestName");
        // These should be bound via custom binding methods
        res.CustomIdPrefix.ShouldBe("PRD");
        res.CustomIdNumber.ShouldBe(123);
    }

    /// <summary>
    /// Tests [JsonExtensionData] attribute in AOT mode.
    /// AOT ISSUE: JsonExtensionData uses reflection to find the extension property.
    /// Dynamic property handling requires runtime type inspection.
    /// Dictionary<string, object> needs runtime type resolution.
    /// </summary>
    [Fact] // AOT ISSUE: JsonExtensionData attribute may not work in Native AOT mode
    public async Task Json_Extension_Data_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = "TestName",
                Id = 123,
                ExtraField1 = "extra1",
                ExtraField2 = 456,
                ExtraField3 = true
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("extension-data-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<ExtensionDataResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("TestName");
        res.Id.ShouldBe(123);
        res.AdditionalFieldCount.ShouldBe(3);
        res.ExtensionDataWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests computed/calculated properties in AOT mode.
    /// AOT ISSUE: Get-only properties with computed values may not serialize.
    /// Expression-bodied members need property getter preservation.
    /// JsonIgnoreCondition evaluation uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Computed properties may not serialize correctly in Native AOT mode
    public async Task Computed_Properties_Work_In_AOT_Mode()
    {
        var req = new ComputedPropertyRequest
        {
            FirstName = "John",
            LastName = "Doe",
            BirthYear = 1990,
            Scores = [85, 90, 78, 92, 88]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ComputedPropertyEndpoint, ComputedPropertyRequest, ComputedPropertyResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FirstName.ShouldBe("John");
        res.LastName.ShouldBe("Doe");
        res.FullName.ShouldBe("John Doe"); // Computed property
        res.Age.ShouldBeGreaterThan(30); // Computed from BirthYear
        res.AverageScore.ShouldBeGreaterThan(80); // Computed
        res.TotalScore.ShouldBe(433); // Computed
        res.ComputedPropertiesWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests various collection types (HashSet, Queue, Stack, etc.) in AOT mode.
    /// AOT ISSUE: Each collection type requires specific JsonConverter.
    /// Activator.CreateInstance for collections uses reflection.
    /// Generic collection interfaces need runtime type resolution.
    /// </summary>
    [Fact] // AOT ISSUE: Various collection types may not deserialize in Native AOT mode
    public async Task Collection_Types_Work_In_AOT_Mode()
    {
        var req = new CollectionTypesRequest
        {
            IntArray = [1, 2, 3],
            StringList = ["a", "b", "c"],
            IntHashSet = [10, 20, 30],
            StringQueue = new Queue<string>(["q1", "q2"]),
            IntStack = new Stack<int>([100, 200]),
            StringLinkedList = new LinkedList<string>(["l1", "l2"]),
            IntObservable = [1000, 2000],
            StringSortedSet = ["z", "a", "m"],
            ImmutableArray = [new() { Name = "item1", Value = 1 }]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<CollectionTypesEndpoint, CollectionTypesRequest, CollectionTypesResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ArrayCount.ShouldBe(3);
        res.ListCount.ShouldBe(3);
        res.HashSetCount.ShouldBe(3);
        res.QueueCount.ShouldBe(2);
        res.StackCount.ShouldBe(2);
        res.LinkedListCount.ShouldBe(2);
        res.AllCollectionsBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests 'required' keyword members in AOT mode.
    /// AOT ISSUE: 'required' keyword enforcement uses constructor parameter analysis.
    /// Required member validation uses reflection.
    /// RequiredMemberAttribute handling needs runtime metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Required keyword members may not work in Native AOT mode
    public async Task Required_Members_Work_In_AOT_Mode()
    {
        var req = new RequiredMembersRequest
        {
            RequiredName = "RequiredTest",
            RequiredId = 999,
            OptionalDescription = "Optional desc",
            RequiredTags = ["tag1", "tag2"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<RequiredMembersEndpoint, RequiredMembersRequest, RequiredMembersResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("RequiredTest");
        res.Id.ShouldBe(999);
        res.RequiredMembersBound.ShouldBeTrue();
    }

    /// <summary>
    /// Tests generic base class with 'new()' constraint in AOT mode.
    /// AOT ISSUE: 'where TEntity : new()' constraint uses Activator.CreateInstance.
    /// Generic type resolution for base class uses MakeGenericType.
    /// Virtual method dispatch in generic base needs runtime resolution.
    /// </summary>
    [Fact] // AOT ISSUE: Generic base with new() constraint may not work in Native AOT mode
    public async Task Generic_Crud_Base_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("generic-crud/42");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<CrudResponse<ProductEntity>>();

        res.ShouldNotBeNull();
        res.Entity.ShouldNotBeNull();
        res.Entity!.Id.ShouldBe(42);
        res.Entity.Name.ShouldContain("42");
        res.EntityTypeName.ShouldBe("ProductEntity");
        res.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests sealed record with primary constructor in AOT mode.
    /// AOT ISSUE: Sealed record with primary constructor needs parameter discovery.
    /// Record deconstruction uses reflection for property mapping.
    /// With expression cloning uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Sealed record with primary constructor may not work in Native AOT mode
    public async Task Sealed_Record_Works_In_AOT_Mode()
    {
        var req = new SealedRecordRequest(
            Name: "SealedTest",
            Value: 100,
            CreatedAt: DateTime.UtcNow,
            Tags: ["sealed", "record"]
        );

        var (rsp, res, err) = await app.Client.POSTAsync<SealedRecordEndpoint, SealedRecordRequest, SealedRecordResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("SealedTest");
        res.Value.ShouldBe(200); // Doubled in handler
        res.TagCount.ShouldBe(2);
        res.ProcessedMessage.ShouldContain("SealedTest");
    }

    /// <summary>
    /// Tests [JsonConverter] attribute on properties in AOT mode.
    /// AOT ISSUE: Property-level JsonConverter uses reflection for discovery.
    /// Custom converter instantiation uses Activator.CreateInstance.
    /// Converter registration per-property needs runtime type inspection.
    /// </summary>
    [Fact] // AOT ISSUE: Property-level JsonConverter may not work in Native AOT mode
    public async Task Custom_Converter_Attribute_Works_In_AOT_Mode()
    {
        var testDate = new DateTime(2025, 6, 15, 10, 30, 45, DateTimeKind.Utc);
        var req = new CustomConverterRequest
        {
            Name = "ConverterTest",
            CustomDate = testDate,
            StandardDate = testDate
        };

        var (rsp, res, err) = await app.Client.POSTAsync<CustomConverterEndpoint, CustomConverterRequest, CustomConverterResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("ConverterTest");
        res.CustomConverterWorked.ShouldBeTrue();
        res.CustomDateFormatted.ShouldBe("2025-06-15 10:30:45");
    }

    /// <summary>
    /// Tests enum with [EnumMember] and [Flags] attributes in AOT mode.
    /// AOT ISSUE: EnumMember attribute discovery uses reflection.
    /// JsonStringEnumConverter needs enum metadata preserved.
    /// Flags enum HasFlag() may use reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Enum with EnumMember and Flags may not work in Native AOT mode
    public async Task Enum_With_Attributes_Works_In_AOT_Mode()
    {
        var req = new EnumSerializationRequest
        {
            Status = StatusWithCustomNames.Active,
            Permissions = PermissionFlags.Read | PermissionFlags.Write,
            StatusList = [StatusWithCustomNames.Pending, StatusWithCustomNames.Completed]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<EnumSerializationEndpoint, EnumSerializationRequest, EnumSerializationResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Status.ShouldBe(StatusWithCustomNames.Active);
        res.HasReadPermission.ShouldBeTrue();
        res.HasWritePermission.ShouldBeTrue();
        res.StatusListCount.ShouldBe(2);
        res.EnumSerializationWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests dynamic/object response types in AOT mode.
    /// AOT ISSUE: Dynamic types use DLR which is reflection-heavy.
    /// ExpandoObject serialization uses runtime type inspection.
    /// Object-typed responses need runtime type discovery.
    /// </summary>
    [Fact] // AOT ISSUE: Dynamic/object response types may not work in Native AOT mode
    public async Task Dynamic_Response_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("dynamic-response?ResponseType=anonymous&Key=testKey&Value=testValue");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldContain("testKey");
        content.ShouldContain("testValue");
    }

    // ==================== ROUND 8 AOT TESTS ====================

    /// <summary>
    /// Tests [JsonPropertyName] attribute for JSON property aliasing in AOT mode.
    /// AOT ISSUE: JsonPropertyName attribute discovery uses reflection.
    /// Property-to-JSON mapping requires GetCustomAttribute calls.
    /// Source generator must preserve these attribute mappings.
    /// </summary>
    [Fact] // AOT ISSUE: JsonPropertyName attribute may not work correctly in Native AOT mode
    public async Task JsonPropertyName_Works_In_AOT_Mode()
    {
        // Send with aliased JSON property names
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                user_name = "TestUser",
                email_address = "test@example.com",
                phone_number = "555-1234",
                Age = 30
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("json-property-name-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<JsonPropertyNameResponse>();

        res.ShouldNotBeNull();
        res.UserName.ShouldBe("TestUser");
        res.EmailAddress.ShouldBe("test@example.com");
        res.Age.ShouldBe(30);
        res.JsonPropertyNameWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [JsonNumberHandling] attribute in AOT mode.
    /// AOT ISSUE: JsonNumberHandling attribute uses reflection for property discovery.
    /// Number parsing with custom handling needs runtime attribute inspection.
    /// Combined flags evaluation requires reflection-based attribute analysis.
    /// </summary>
    [Fact] // AOT ISSUE: JsonNumberHandling attribute may not work in Native AOT mode
    public async Task JsonNumberHandling_Works_In_AOT_Mode()
    {
        // Send numbers as strings
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                IntFromString = "42",
                DoubleFromString = "3.14",
                DecimalFromString = "99.99",
                LongAsString = 1234567890,
                BothWays = "100"
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("json-number-handling-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<JsonNumberHandlingResponse>();

        res.ShouldNotBeNull();
        res.IntFromString.ShouldBe(42);
        res.DoubleFromString.ShouldBe(3.14);
        res.NumberHandlingWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [JsonConstructor] attribute for parameterized constructors in AOT mode.
    /// AOT ISSUE: JsonConstructor discovery uses reflection to find marked constructors.
    /// Constructor parameter mapping requires ParameterInfo reflection.
    /// Immutable types with constructor-only initialization need runtime analysis.
    /// </summary>
    [Fact] // AOT ISSUE: JsonConstructor attribute may not work in Native AOT mode
    public async Task JsonConstructor_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                name = "TestName",
                value = 42,
                Description = "Test description"
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("json-constructor-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<JsonConstructorResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("TestName");
        res.Value.ShouldBe(42);
        res.JsonConstructorWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests immutable types with JsonConstructor in AOT mode.
    /// AOT ISSUE: Immutable type deserialization needs constructor parameter binding.
    /// IReadOnlyList initialization at construction time uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Immutable types with JsonConstructor may not work in Native AOT mode
    public async Task Immutable_Type_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                id = "123",
                title = "Test Title",
                tags = new[] { "tag1", "tag2", "tag3" }
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("immutable-type-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<JsonConstructorResponse>();

        res.ShouldNotBeNull();
        res.ImmutableId.ShouldBe("123");
        res.ImmutableTitle.ShouldBe("Test Title");
        res.TagCount.ShouldBe(3);
        res.JsonConstructorWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [JsonDerivedType] and [JsonPolymorphic] attributes in AOT mode.
    /// AOT ISSUE: Polymorphic deserialization uses type discriminator mapping.
    /// JsonDerivedType attribute discovery requires reflection.
    /// Runtime type resolution for derived types uses Type.GetType.
    /// </summary>
    [Fact] // AOT ISSUE: JsonDerivedType/JsonPolymorphic may not work in Native AOT mode
    public async Task JsonDerivedType_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Animal = new
                {
                    @__type = "dog",
                    Name = "Buddy",
                    Age = 5,
                    Breed = "Labrador"
                },
                Animals = new object[]
                {
                    new { @__type = "cat", Name = "Whiskers", Age = 3, IsIndoor = true },
                    new { @__type = "bird", Name = "Tweety", Age = 1, Wingspan = 0.5 }
                }
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("json-derived-type-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<JsonDerivedTypeResponse>();

        res.ShouldNotBeNull();
        res.JsonDerivedTypeWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [JsonInclude] with private setters in AOT mode.
    /// AOT ISSUE: Private setter access requires reflection with BindingFlags.NonPublic.
    /// JsonInclude attribute discovery uses GetCustomAttribute.
    /// </summary>
    [Fact] // AOT ISSUE: Private setters with JsonInclude may not work in Native AOT mode
    public async Task Private_Setter_Works_In_AOT_Mode()
    {
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Name = "PrivateTest",
                Value = 42,
                CreatedAt = DateTime.UtcNow,
                PublicProperty = "PublicValue"
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var rsp = await app.Client.PostAsync("private-setter-test", content);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<PrivateSetterResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("PrivateTest");
        res.Value.ShouldBe(42);
        res.PrivateSetterWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests init-only properties in AOT mode.
    /// AOT ISSUE: Init-only setters need special handling during deserialization.
    /// Compiler generates modreq for init setters which needs metadata preservation.
    /// </summary>
    [Fact] // AOT ISSUE: Init-only properties may not work in Native AOT mode
    public async Task Init_Only_Properties_Work_In_AOT_Mode()
    {
        var req = new InitOnlyRequest
        {
            Name = "InitTest",
            Value = 100,
            Items = ["item1", "item2"],
            Nested = new NestedInitOnly
            {
                Description = "Nested desc",
                Score = 95.5
            }
        };

        var (rsp, res, err) = await app.Client.POSTAsync<InitOnlyEndpoint, InitOnlyRequest, InitOnlyResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Name.ShouldBe("InitTest");
        res.Value.ShouldBe(100);
        res.ItemCount.ShouldBe(2);
        res.InitOnlyWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests required init properties in AOT mode.
    /// AOT ISSUE: Required modifier needs SetsRequiredMembersAttribute handling.
    /// Validation of required properties at runtime needs metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Required init properties may not work in Native AOT mode
    public async Task Required_Init_Properties_Work_In_AOT_Mode()
    {
        var req = new RequiredInitRequest
        {
            RequiredName = "RequiredInitTest",
            RequiredValue = 999,
            OptionalField = "Optional"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<RequiredInitEndpoint, RequiredInitRequest, RequiredInitResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.RequiredName.ShouldBe("RequiredInitTest");
        res.RequiredValue.ShouldBe(999);
        res.RequiredInitWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests anonymous type serialization in AOT mode.
    /// AOT ISSUE: Anonymous types are compiler-generated and not registered.
    /// typeof(anonymousType) at runtime fails without metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Anonymous type serialization may not work in Native AOT mode
    public async Task Anonymous_Type_Works_In_AOT_Mode()
    {
        var req = new AnonymousTypeRequest
        {
            Name = "AnonTest",
            Count = 5,
            Tags = ["tag1", "tag2"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<AnonymousTypeEndpoint, AnonymousTypeRequest, object>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldContain("AnonTest");
        content.ShouldContain("AnonymousTypeWorked");
    }

    /// <summary>
    /// Tests nested anonymous types in AOT mode.
    /// AOT ISSUE: Nested anonymous types create multiple compiler-generated types.
    /// Each nesting level creates a new unregistered type.
    /// </summary>
    [Fact] // AOT ISSUE: Nested anonymous types may not work in Native AOT mode
    public async Task Nested_Anonymous_Type_Works_In_AOT_Mode()
    {
        var req = new AnonymousTypeRequest
        {
            Name = "NestedAnonTest",
            Count = 10,
            Tags = ["a", "b", "c"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<NestedAnonymousEndpoint, AnonymousTypeRequest, object>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var content = await rsp.Content.ReadAsStringAsync();
        content.ShouldContain("NestedAnonTest");
        content.ShouldContain("Outer");
    }

    /// <summary>
    /// Tests Lazy&lt;T&gt; property handling in AOT mode.
    /// AOT ISSUE: Lazy&lt;T&gt; uses reflection for value factory invocation.
    /// Generic type instantiation at runtime may fail.
    /// </summary>
    [Fact] // AOT ISSUE: Lazy<T> properties may not work in Native AOT mode
    public async Task Lazy_Loading_Works_In_AOT_Mode()
    {
        var rsp = await app.Client.GetAsync("lazy-loading-test?Name=LazyTest&Id=42");

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail($"Request failed: {await rsp.Content.ReadAsStringAsync()}");

        var res = await rsp.Content.ReadFromJsonAsync<LazyLoadingResponse>();

        res.ShouldNotBeNull();
        res.Name.ShouldBe("LazyTest");
        res.Id.ShouldBe(42);
        res.ComputedValue.ShouldContain("LazyTest");
        res.LazyWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests expression tree compilation in AOT mode.
    /// AOT ISSUE: Expression.Compile() requires JIT compilation.
    /// Dynamic expression building uses System.Reflection.Emit.
    /// Lambda expression compilation at runtime is not supported in AOT.
    /// </summary>
    [Fact] // AOT ISSUE: Expression tree compilation fails in Native AOT mode
    public async Task Expression_Tree_Works_In_AOT_Mode()
    {
        var req = new ExpressionTreeRequest
        {
            PropertyName = "IsActive",
            Value = 1,
            Items =
            [
                new() { Id = 1, Name = "Active1", Price = 10, IsActive = true },
                new() { Id = 2, Name = "Inactive", Price = 20, IsActive = false },
                new() { Id = 3, Name = "Active2", Price = 30, IsActive = true }
            ]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ExpressionTreeEndpoint, ExpressionTreeRequest, ExpressionTreeResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FilteredCount.ShouldBe(2); // Only active items
        res.ExpressionTreeWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests dynamic LINQ with reflection in AOT mode.
    /// AOT ISSUE: PropertyInfo.GetValue uses reflection.
    /// Dynamic ordering by property name needs runtime metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Dynamic LINQ operations may not work in Native AOT mode
    public async Task Dynamic_Linq_Works_In_AOT_Mode()
    {
        var req = new ExpressionTreeRequest
        {
            PropertyName = "Name",
            Items =
            [
                new() { Id = 1, Name = "Zebra", Price = 30, IsActive = true },
                new() { Id = 2, Name = "Apple", Price = 10, IsActive = true },
                new() { Id = 3, Name = "Mango", Price = 20, IsActive = true }
            ]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DynamicLinqEndpoint, ExpressionTreeRequest, ExpressionTreeResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ExpressionTreeWorked.ShouldBeTrue();
        res.FilteredNames.First().ShouldBe("Apple"); // Should be sorted by Name
    }

    /// <summary>
    /// Tests reflection-based type inspection in AOT mode.
    /// AOT ISSUE: Type.GetType() with string fails without metadata.
    /// GetProperties/GetMethods may return incomplete results.
    /// </summary>
    [Fact] // AOT ISSUE: Reflection-based type inspection fails in Native AOT mode
    public async Task Reflection_Type_Inspection_Works_In_AOT_Mode()
    {
        var req = new ReflectionRequest
        {
            TypeName = "NativeAotChecker.Endpoints.ReflectionTestClass",
            PropertyName = "Name"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ReflectionEndpoint, ReflectionRequest, ReflectionResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.PropertyCount.ShouldBeGreaterThan(0);
        res.PropertyNames.ShouldContain("Name");
        res.ReflectionWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests dynamic instance creation in AOT mode.
    /// AOT ISSUE: Activator.CreateInstance uses reflection.
    /// Constructor discovery requires metadata preservation.
    /// </summary>
    [Fact] // AOT ISSUE: Activator.CreateInstance fails in Native AOT mode
    public async Task Dynamic_Creation_Works_In_AOT_Mode()
    {
        var req = new ReflectionRequest
        {
            PropertyName = "Name"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DynamicCreationEndpoint, ReflectionRequest, ReflectionResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ReflectionWorked.ShouldBeTrue();
        res.PropertyValue.ShouldBe("Default");
    }

    /// <summary>
    /// Tests dynamic method invocation in AOT mode.
    /// AOT ISSUE: MethodInfo.Invoke uses reflection.
    /// Parameter binding at runtime needs metadata.
    /// </summary>
    [Fact] // AOT ISSUE: MethodInfo.Invoke fails in Native AOT mode
    public async Task Dynamic_Invocation_Works_In_AOT_Mode()
    {
        var req = new ReflectionRequest
        {
            MethodName = "GetFullInfo"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DynamicInvocationEndpoint, ReflectionRequest, ReflectionResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ReflectionWorked.ShouldBeTrue();
        res.PropertyValue.ShouldContain("Test");
    }

    /// <summary>
    /// Tests generic method invocation via MakeGenericMethod in AOT mode.
    /// AOT ISSUE: MakeGenericMethod requires runtime type composition.
    /// Open generic method invocation uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: MakeGenericMethod fails in Native AOT mode
    public async Task Generic_Method_Works_In_AOT_Mode()
    {
        var req = new GenericMethodRequest
        {
            TypeName = "string",
            Value = "TestValue"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericMethodEndpoint, GenericMethodRequest, GenericMethodResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.TypeUsed.ShouldBe("String");
        res.Result.ShouldContain("Processed as String");
        res.GenericMethodWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests generic type instantiation at runtime in AOT mode.
    /// AOT ISSUE: Type.MakeGenericType uses runtime type composition.
    /// Closed generic type creation at runtime is not AOT-safe.
    /// </summary>
    [Fact] // AOT ISSUE: MakeGenericType fails in Native AOT mode
    public async Task Generic_Type_Instantiation_Works_In_AOT_Mode()
    {
        var req = new GenericMethodRequest
        {
            TypeName = "string",
            StringItems = ["item1", "item2", "item3"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<GenericTypeEndpoint, GenericMethodRequest, GenericMethodResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ProcessedCount.ShouldBe(3);
        res.GenericMethodWorked.ShouldBeTrue();
    }

    #region Round 9 Tests - Advanced Serialization and Runtime Patterns

    /// <summary>
    /// Tests ValueTuple serialization in AOT mode.
    /// AOT ISSUE: ValueTuple generic types need source generation for each arity.
    /// Named tuple element names are stored in TupleElementNamesAttribute.
    /// </summary>
    [Fact] // AOT ISSUE: ValueTuple serialization may fail
    public async Task ValueTuple_Serialization_Works_In_AOT_Mode()
    {
        var req = new ValueTupleRequest
        {
            Person = ("John Doe", 30),
            Coordinates = (10, 20, 30),
            UnnamedTuple = ("A", "B", "C"),
            TupleList = [(1, "One"), (2, "Two")]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ValueTupleEndpoint, ValueTupleRequest, ValueTupleResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.PersonName.ShouldBe("John Doe");
        res.PersonAge.ShouldBe(30);
        res.CoordinateSum.ShouldBe(60);
        res.TupleListCount.ShouldBe(2);
        res.ValueTupleWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests DateOnly and TimeOnly types in AOT mode.
    /// AOT ISSUE: DateOnly/TimeOnly are newer types needing explicit converter support.
    /// JSON serialization of these types requires specific converters.
    /// </summary>
    [Fact] // AOT ISSUE: DateOnly/TimeOnly serialization needs explicit support
    public async Task DateOnly_TimeOnly_Work_In_AOT_Mode()
    {
        var req = new DateOnlyTimeOnlyRequest
        {
            BirthDate = new DateOnly(1990, 5, 15),
            StartTime = new TimeOnly(14, 30, 0),
            OptionalDate = new DateOnly(2025, 1, 1),
            OptionalTime = new TimeOnly(9, 0, 0),
            ImportantDates = [new DateOnly(2024, 12, 25), new DateOnly(2025, 1, 1)]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DateOnlyTimeOnlyEndpoint, DateOnlyTimeOnlyRequest, DateOnlyTimeOnlyResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.BirthDate.ShouldBe(new DateOnly(1990, 5, 15));
        res.StartTime.ShouldBe(new TimeOnly(14, 30, 0));
        res.DayOfYear.ShouldBe(135);
        res.Hour.ShouldBe(14);
        res.ImportantDatesCount.ShouldBe(2);
        res.DateOnlyWorked.ShouldBeTrue();
        res.TimeOnlyWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Int128, UInt128, and Half numeric types in AOT mode.
    /// AOT ISSUE: Int128/UInt128 are not natively supported by JSON serialization.
    /// Half precision requires custom converter.
    /// </summary>
    [Fact] // AOT ISSUE: Large numeric types may not serialize correctly
    public async Task Large_Numeric_Types_Work_In_AOT_Mode()
    {
        var req = new LargeNumericRequest
        {
            LargeInt = 123456789012345678,
            LargeUInt = 987654321098765432,
            HalfPrecision = (Half)3.14,
            BigDecimal = 12345678901234567890.12345678901234567890m
        };

        var (rsp, res, err) = await app.Client.POSTAsync<LargeNumericEndpoint, LargeNumericRequest, LargeNumericResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.LargeIntString.ShouldNotBeNullOrEmpty();
        res.LargeNumericWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests IAsyncEnumerable streaming in AOT mode.
    /// AOT ISSUE: IAsyncEnumerable state machine generation at runtime.
    /// Yield return compilation needs JIT for state machine.
    /// </summary>
    [Fact] // AOT ISSUE: IAsyncEnumerable may fail in AOT
    public async Task AsyncEnumerable_Streaming_Works_In_AOT_Mode()
    {
        var response = await app.Client.GetAsync("/async-enumerable-test?count=3");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests JsonDocument and JsonElement handling in AOT mode.
    /// AOT ISSUE: JsonDocument parsing is dynamic by nature.
    /// JsonElement property enumeration uses runtime type inspection.
    /// </summary>
    [Fact] // AOT ISSUE: JsonDocument/JsonElement dynamic handling
    public async Task JsonDocument_Works_In_AOT_Mode()
    {
        var json = """{"name":"Test","value":42,"nested":{"inner":"data"}}""";
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await app.Client.PostAsync("/json-document-test", content);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        result.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests Delegate.DynamicInvoke in AOT mode.
    /// AOT ISSUE: DynamicInvoke uses reflection for parameter binding.
    /// Late-bound delegate invocation needs runtime type resolution.
    /// </summary>
    [Fact] // AOT ISSUE: DynamicInvoke uses reflection
    public async Task Delegate_DynamicInvoke_Works_In_AOT_Mode()
    {
        var req = new DelegateInvokeRequest
        {
            Operation = "add",
            ValueA = 10,
            ValueB = 20
        };

        var (rsp, res, err) = await app.Client.POSTAsync<DelegateInvokeEndpoint, DelegateInvokeRequest, DelegateInvokeResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Result.ShouldBe(30);
        res.DelegateInvokeWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests covariant/contravariant generic interfaces in AOT mode.
    /// AOT ISSUE: Variance handling requires runtime type checking.
    /// Generic interface dispatch with variance needs runtime resolution.
    /// </summary>
    [Fact] // AOT ISSUE: Covariant/contravariant generics need runtime support
    public async Task Covariant_Interfaces_Work_In_AOT_Mode()
    {
        var req = new CovariantRequest
        {
            Entities = [
                new BaseEntity { Id = 1, Name = "Entity1" },
                new DerivedEntity { Id = 2, Name = "Entity2", ExtraField = "Extra" }
            ],
            EntityId = 1
        };

        var (rsp, res, err) = await app.Client.POSTAsync<CovariantEndpoint, CovariantRequest, CovariantResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.EntityCount.ShouldBe(2);
        res.EntityNames.ShouldContain("Entity1");
        res.EntityNames.ShouldContain("Entity2");
        res.CovariantWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests record 'with' expression copying in AOT mode.
    /// AOT ISSUE: With expression creates new instance via hidden Clone method.
    /// Record copy constructor uses reflection for property mapping.
    /// </summary>
    [Fact] // AOT ISSUE: Record 'with' expression may use reflection
    public async Task Record_Copy_With_Expression_Works_In_AOT_Mode()
    {
        var req = new RecordCopyRequest
        {
            Person = new PersonRecord("John", "Doe", 30),
            Address = new AddressRecord { Street = "123 Main St", City = "OldCity", ZipCode = "12345", Country = "USA" },
            NewLastName = "Smith",
            NewCity = "NewCity"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<RecordCopyEndpoint, RecordCopyRequest, RecordCopyResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.OriginalFullName.ShouldBe("John Doe");
        res.ModifiedFullName.ShouldBe("John Smith");
        res.OriginalCity.ShouldBe("OldCity");
        res.ModifiedCity.ShouldBe("NewCity");
        res.RecordCopyWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests type pattern matching in AOT mode.
    /// AOT ISSUE: Type patterns use 'is' and 'as' which need runtime type checks.
    /// Switch expressions on types use type metadata.
    /// </summary>
    [Fact] // AOT ISSUE: Pattern matching on types uses runtime type info
    public async Task Pattern_Matching_Works_In_AOT_Mode()
    {
        var req = new PatternMatchingRequest
        {
            Shape = new CircleShape { Color = "Red", Radius = 5.0 },
            Shapes = [
                new CircleShape { Color = "Red", Radius = 5.0 },
                new RectangleShape { Color = "Blue", Width = 4.0, Height = 3.0 },
                new TriangleShape { Color = "Green", Base = 6.0, Height = 4.0 }
            ]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<PatternMatchingEndpoint, PatternMatchingRequest, PatternMatchingResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ShapeType.ShouldBe("CircleShape");
        res.Area.ShouldBeGreaterThan(0);
        res.ShapeCounts["circles"].ShouldBe(1);
        res.ShapeCounts["rectangles"].ShouldBe(1);
        res.ShapeCounts["triangles"].ShouldBe(1);
        res.PatternMatchingWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests TimeZoneInfo and time conversions in AOT mode.
    /// AOT ISSUE: TimeZoneInfo.FindSystemTimeZoneById uses registry/file lookup.
    /// Time zone database access may need runtime resources.
    /// </summary>
    [Fact] // AOT ISSUE: TimeZoneInfo lookup may fail
    public async Task TimeZone_Conversion_Works_In_AOT_Mode()
    {
        var req = new TimeProviderRequest
        {
            TimeZoneId = "Pacific Standard Time",
            UtcDateTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            DateTimeOffset = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero)
        };

        var (rsp, res, err) = await app.Client.POSTAsync<TimeProviderEndpoint, TimeProviderRequest, TimeProviderResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.TimeZoneName.ShouldNotBeNullOrEmpty();
        res.TimeProviderWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests reflection-based object-to-object mapping in AOT mode.
    /// AOT ISSUE: Property enumeration uses GetProperties().
    /// PropertyInfo.SetValue/GetValue use reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Reflection-based mapping fails in AOT
    public async Task Object_Mapper_Works_In_AOT_Mode()
    {
        var req = new SourceDto
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            CreatedAt = new DateTime(2025, 1, 15),
            Tags = ["tag1", "tag2", "tag3"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ObjectToObjectMapperEndpoint, SourceDto, ObjectMapperResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.MappedObject.ShouldNotBeNull();
        res.MappedObject!.Id.ShouldBe(1);
        res.MappedObject.FullName.ShouldBe("John Doe");
        res.ObjectMappingWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests FormattableString and culture-specific formatting in AOT mode.
    /// AOT ISSUE: FormattableString.Invariant uses runtime culture data.
    /// Culture-specific formatting needs locale resources.
    /// </summary>
    [Fact] // AOT ISSUE: Culture-specific formatting may need resources
    public async Task FormattableString_Works_In_AOT_Mode()
    {
        var req = new FormattableStringRequest
        {
            Name = "TestUser",
            Amount = 1234.56m,
            Date = new DateTime(2025, 6, 15),
            Culture = "en-US"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<FormattableStringEndpoint, FormattableStringRequest, FormattableStringResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FormattedDefault.ShouldNotBeNullOrEmpty();
        res.FormattedInvariant.ShouldNotBeNullOrEmpty();
        res.FormattableStringWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests culture-sensitive string operations in AOT mode.
    /// AOT ISSUE: Culture-sensitive comparisons need ICU or NLS data.
    /// CompareInfo.GetCompareInfo uses reflection for culture lookup.
    /// </summary>
    [Fact] // AOT ISSUE: Culture-sensitive string operations need ICU data
    public async Task String_Comparison_Works_In_AOT_Mode()
    {
        var req = new StringComparisonRequest
        {
            StringA = "Strasse",
            StringB = "Straße",
            Culture = "de-DE",
            IgnoreCase = true
        };

        var (rsp, res, err) = await app.Client.POSTAsync<StringComparisonEndpoint, StringComparisonRequest, StringComparisonResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.StringComparisonWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests System.Threading.Channels in AOT mode.
    /// AOT ISSUE: Channel&lt;T&gt; generic instantiation at runtime.
    /// AsyncEnumerable from channel needs state machine generation.
    /// </summary>
    [Fact] // AOT ISSUE: Channel generic instantiation
    public async Task Channel_Works_In_AOT_Mode()
    {
        var req = new ChannelRequest
        {
            ItemCount = 5,
            ChannelCapacity = 10,
            Items = ["item1", "item2", "item3", "item4", "item5"]
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ChannelEndpoint, ChannelRequest, ChannelResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ItemsWritten.ShouldBe(5);
        res.ItemsRead.ShouldBe(5);
        res.ReadItems.Count.ShouldBe(5);
        res.ChannelWorked.ShouldBeTrue();
    }

    #endregion

    #region Round 10 - FastEndpoints Features from Documentation

    /// <summary>
    /// Tests ProcessorState&lt;TState&gt; for shared state between processors in AOT mode.
    /// AOT ISSUE: ProcessorState&lt;TState&gt; requires generic instantiation.
    /// State sharing between pre/post processors needs runtime wiring.
    /// </summary>
    [Fact] // AOT ISSUE: ProcessorState generic instantiation
    public async Task ProcessorState_Works_In_AOT_Mode()
    {
        var req = new ProcessorStateRequest
        {
            Name = "TestState",
            Value = 10
        };

        var (rsp, res, err) = await app.Client.POSTAsync<ProcessorStateEndpoint, ProcessorStateRequest, ProcessorStateResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ProcessorStateWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Validator&lt;TRequest&gt; with FluentValidation expression trees in AOT mode.
    /// AOT ISSUE: RuleFor(x =&gt; x.Property) uses expression tree compilation.
    /// Expression trees are not AOT-compatible without manual handling.
    /// </summary>
    [Fact] // AOT ISSUE: FluentValidation expression tree compilation
    public async Task FluentValidator_Works_In_AOT_Mode()
    {
        var req = new FluentValidatorRequest
        {
            Email = "test@example.com",
            Password = "Test1234!",
            Age = 25,
            PhoneNumber = "+1234567890"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<FluentValidatorEndpoint, FluentValidatorRequest, FluentValidatorResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [FromClaim] attribute binding in AOT mode.
    /// AOT ISSUE: [FromClaim] attribute discovery uses reflection.
    /// Claim value extraction and property binding uses reflection.
    /// </summary>
    [Fact] // AOT ISSUE: FromClaim attribute reflection-based discovery
    public async Task FromClaim_Binding_Works_In_AOT_Mode()
    {
        var req = new FromClaimRequest
        {
            AdditionalData = "resource-123"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<FromClaimEndpoint, FromClaimRequest, FromClaimResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FromClaimWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests IPlainTextRequest interface for raw body content in AOT mode.
    /// AOT ISSUE: IPlainTextRequest interface detection uses 'is' pattern matching.
    /// Interface-based dispatch may use reflection for type checking.
    /// </summary>
    [Fact] // AOT ISSUE: Interface detection and raw body handling
    public async Task PlainTextRequest_Works_In_AOT_Mode()
    {
        var plainText = "This is raw plain text content for testing";
        var req = new PlainTextAotRequest
        {
            Content = plainText,
            DocumentId = 123,
            Format = "text/plain"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<PlainTextRequestEndpoint, PlainTextAotRequest, PlainTextAotResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.PlainTextWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [FromQuery] complex nested object binding in AOT mode.
    /// AOT ISSUE: Complex nested object binding uses reflection for property discovery.
    /// Nested type instantiation requires Activator.CreateInstance.
    /// </summary>
    [Fact] // AOT ISSUE: Complex query parameter binding with nested types
    public async Task ComplexQuery_Binding_Works_In_AOT_Mode()
    {
        var req = new ComplexQueryRequest
        {
            SortBy = "price",
            Ascending = true,
            Page = 1,
            PageSize = 10,
            Filter = new SearchFilter
            {
                MinPrice = 10.0m,
                MaxPrice = 100.0m,
                Category = "Electronics",
                Tags = ["tag1", "tag2"]
            }
        };

        var (rsp, res, err) = await app.Client.GETAsync<QueryParamBindingEndpoint, ComplexQueryRequest, ComplexQueryResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ComplexQueryWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Group&lt;TGroup&gt; and SubGroup&lt;TParent&gt; endpoint grouping in AOT mode.
    /// AOT ISSUE: Group inheritance uses reflection for parent discovery.
    /// Route prefix composition requires runtime type inspection.
    /// </summary>
    [Fact] // AOT ISSUE: Endpoint group inheritance and route composition
    public async Task EndpointGroup_Works_In_AOT_Mode()
    {
        var req = new GroupedEndpointRequest
        {
            UserId = "user-123",
            Action = "test"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<EndpointGroupEndpoint, GroupedEndpointRequest, GroupedEndpointResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.GroupWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests [DontBind], [QueryParam], [RouteParam] source-specific binding in AOT mode.
    /// AOT ISSUE: [DontBind] attribute discovery uses reflection.
    /// Source-specific binding attributes require property metadata inspection.
    /// </summary>
    [Fact] // AOT ISSUE: Source-specific binding attribute discovery
    public async Task DontBind_Attribute_Works_In_AOT_Mode()
    {
        var req = new DontBindRequest
        {
            InternalId = "should-be-ignored",
            ExternalId = "ext-123",
            QueryOnly = "query-value",
            RouteOnly = "route-value"
        };

        var (rsp, res, err) = await app.Client.GETAsync<DontBindEndpoint, DontBindRequest, DontBindResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.DontBindWorked.ShouldBeTrue();
    }

    /// <summary>
    /// Tests Results&lt;T1,T2,...&gt; union types in AOT mode.
    /// AOT ISSUE: Results union type uses generic type composition.
    /// TypedResults static methods create runtime type instances.
    /// </summary>
    [Fact] // AOT ISSUE: Union type generic composition
    public async Task TypedResultsUnion_Works_In_AOT_Mode()
    {
        var req = new TypedResultsUnionRequest
        {
            Id = 42,
            Action = "test-action"
        };

        var (rsp, res, err) = await app.Client.POSTAsync<TypedResultsUnionEndpoint2, TypedResultsUnionRequest, TypedResultsUnionResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.TypedResultsWorked.ShouldBeTrue();
    }

    #endregion
}