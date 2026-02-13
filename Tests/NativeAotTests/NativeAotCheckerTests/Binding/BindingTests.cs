using System.Net;
using NativeAotChecker.Endpoints.Binding;

namespace NativeAotCheckerTests;

public class BindingTests(App app)
{
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
        res.UserId.ShouldBe("0001");
        res.FormValue.ShouldBe("Hello from form");
        res.RequestId.ShouldBe("test-req-789");
    }

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

    [Fact]
    public async Task Complex_Query_Binding_FromQuery_Attribute()
    {
        var editorId = Guid.NewGuid();
        var author1Id = Guid.NewGuid();
        var author2Id = Guid.NewGuid();

        var url = $"/complex-query-binding?Title=Test+Book+Title&BarCodes=12345&BarCodes=54321&Editor.Id={editorId}&" +
                  $"Editor.Name=John+Doe&Authors[0].Id={author1Id}&Authors[0].Name=Author+One&Authors[1].Id={author2Id}&" +
                  $"Authors[1].Name=Author+Two";

        var (rsp, res, err) = await app.Client.GETAsync<ComplexQueryBindingRequest, Book>(url, new());

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Title.ShouldBe("Test Book Title");
        res.BarCodes.Count.ShouldBe(2);
        res.BarCodes[0].ShouldBe(12345);
        res.BarCodes[1].ShouldBe(54321);
        res.Editor.Id.ShouldBe(editorId);
        res.Editor.Name.ShouldBe("John Doe");
        res.Authors.Count.ShouldBe(2);
        res.Authors[0].Id.ShouldBe(author1Id);
        res.Authors[0].Name.ShouldBe("Author One");
        res.Authors[1].Id.ShouldBe(author2Id);
        res.Authors[1].Name.ShouldBe("Author Two");
    }

    [Fact]
    public async Task Custom_Endpoint_Level_Binder()
    {
        var id = Guid.NewGuid().ToString();
        app.Client.DefaultRequestHeaders.Add("X-Custom-Value", id);

        var (rsp, res, err) = await app.Client.POSTAsync<CustomBinderEndpoint, CustomBinderRequest, CustomBinderResponse>(
                                  new()
                                  {
                                      InputValue = "ignored",
                                      ProcessedValue = "ignored"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.InputValue.ShouldBe(id);
        res.ProcessedValue.ShouldBe($"CUSTOM-BINDER:{id}");
        res.BinderWasUsed.ShouldBeTrue();
    }

    [Fact]
    public async Task Nullable_Bool_Query_Parameter_Binding()
    {
        var rsp1 = await app.Client.GetAsync("nullable-bool-query-test?NonNullableBool=true");
        rsp1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (rsp2, res2, err2) = await app.Client.GETAsync<NullableBoolQueryEndpoint, NullableBoolQueryRequest, NullableBoolQueryResponse>(
                                     new()
                                     {
                                         NonNullableBool = true,
                                         NullableBool = true
                                     });

        if (!rsp2.IsSuccessStatusCode)
            Assert.Fail(err2);

        res2.NonNullableBool.ShouldBeTrue();
        res2.NullableBool.ShouldBe(true);
    }

    [Fact]
    public async Task Override_Client_Request_Header_In_Test_Request()
    {
        const string tenantId = "tenant-123";
        var correlationId = Guid.NewGuid().ToString();

        app.Client.DefaultRequestHeaders.Add("x-correlation-id", correlationId);
        app.Client.DefaultRequestHeaders.Add("x-tenant-id", tenantId);

        var (rsp, res, err) = await app.Client.GETAsync<FromHeaderBindingEndpoint, FromHeaderRequest, FromHeaderResponse>(
                                  new()
                                  {
                                      CorrelationId = "this-should-override-default-correlation-id",
                                      TenantId = null!
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.CorrelationId.ShouldBe("this-should-override-default-correlation-id");
        res.TenantId.ShouldBe(tenantId);
        res.AllHeadersBound.ShouldBeTrue();
    }

    [Fact]
    public async Task Binding_With_BindFrom_Attribute()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<BindFromEndpoint, BindFromRequest, BindFromResponse>(
                                  new()
                                  {
                                      CustomerId = 123,
                                      ProductName = "Test Product",
                                      Quantity = 5,
                                      Category = "electronics"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.ShouldNotBeNull();
        res.CustomerId.ShouldBe(123);
        res.ProductName.ShouldBe("Test Product");
        res.Quantity.ShouldBe(5);
        res.Category.ShouldBe("electronics");
        res.AllBindingsWorked.ShouldBeTrue();
    }

    [Fact]
    public async Task Struct_Type_Dto_Binding()
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
}