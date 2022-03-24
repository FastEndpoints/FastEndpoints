using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using TestCases.EventHandlingTest;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class MiscTestCases
    {
        [TestMethod]
        public async Task MultiVerbEndpointAnonymousUserPutFail()
        {
            using var imageContent = new ByteArrayContent(Array.Empty<byte>());
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            using var form = new MultipartFormDataContent { { imageContent, "File", "test.png" } };

            var res = await GuestClient.PutAsync("/api/uploads/image/save", form);

            Assert.AreEqual(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        [TestMethod]
        public async Task ClaimMissing()
        {
            var (_, result) = await AdminClient.POSTAsync<
                TestCases.MissingClaimTest.ThrowIfMissingEndpoint,
                TestCases.MissingClaimTest.ThrowIfMissingRequest,
                ErrorResponse>(new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(400, result?.StatusCode);
            Assert.AreEqual(1, result?.Errors.Count);
            Assert.IsTrue(result?.Errors.ContainsKey("null-claim"));
        }

        [TestMethod]
        public async Task ClaimMissingButDontThrow()
        {
            var (res, result) = await AdminClient.POSTAsync<
                TestCases.MissingClaimTest.DontThrowIfMissingEndpoint,
                TestCases.MissingClaimTest.DontThrowIfMissingRequest,
                string>(new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.AreEqual("you sent xyz", result);
        }

        [TestMethod]
        public async Task HeaderMissing()
        {
            var (_, result) = await AdminClient.POSTAsync<
                TestCases.MissingHeaderTest.ThrowIfMissingEndpoint,
                TestCases.MissingHeaderTest.ThrowIfMissingRequest,
                ErrorResponse>(new()
                {
                    TenantID = "abc"
                });

            Assert.AreEqual(400, result?.StatusCode);
            Assert.AreEqual(1, result?.Errors.Count);
            Assert.IsTrue(result?.Errors.ContainsKey("TenantID"));
        }

        [TestMethod]
        public async Task HeaderMissingButDontThrow()
        {
            var (res, result) = await AdminClient.POSTAsync<
                TestCases.MissingHeaderTest.DontThrowIfMissingEndpoint,
                TestCases.MissingHeaderTest.DontThrowIfMissingRequest,
                string>(new()
                {
                    TenantID = "abc"
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.AreEqual("you sent abc", result);
        }

        [TestMethod]
        public async Task RouteValueReadingInEndpointWithoutRequest()
        {
            var (rsp, res) = await GuestClient.GETAsync<
                EmptyRequest,
                TestCases.RouteBindingInEpWithoutReq.Response>(
                "/api/test-cases/ep-witout-req-route-binding-test/09809/12", new());

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual(09809, res!.CustomerID);
            Assert.AreEqual(12, res!.OtherID);
        }

        [TestMethod]
        public async Task RouteValueReadingIsRequired()
        {
            var (rsp, res) = await GuestClient.GETAsync<
                EmptyRequest,
                ErrorResponse>(
                "/api/test-cases/ep-witout-req-route-binding-test/09809/lkjhlkjh", new());

            Assert.AreEqual(HttpStatusCode.BadRequest, rsp?.StatusCode);
            Assert.IsTrue(res?.Errors.ContainsKey("OtherID"));
        }

        [TestMethod]
        public async Task RouteValueBinding()
        {
            var (rsp, res) = await GuestClient.POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(

                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45?Url=https://test.com&Custom=12",

                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing",
                    Custom = new() { Value = 11111 }
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("something", res?.String);
            Assert.AreEqual(true, res?.Bool);
            Assert.AreEqual(99, res?.Int);
            Assert.AreEqual(483752874564876, res?.Long);
            Assert.AreEqual(2232.12, res?.Double);
            Assert.AreEqual("from body value", res?.FromBody);
            Assert.AreEqual(123.45m, res?.Decimal);
            Assert.AreEqual("https://test.com/", res?.Url);
            Assert.AreEqual(12, res?.Custom.Value);
        }

        [TestMethod]
        public async Task RouteValueBindingFromQueryParams()
        {
            var (rsp, res) = await GuestClient.POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(

                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything",

                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing"
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("everything", res?.String);
            Assert.AreEqual(false, res?.Bool);
            Assert.AreEqual(99, res?.Int);
            Assert.AreEqual(483752874564876, res?.Long);
            Assert.AreEqual(2232.12, res?.Double);
            Assert.AreEqual("from body value", res?.FromBody);
            Assert.AreEqual(123.45m, res?.Decimal);
            Assert.AreEqual(null, res?.Blank);
        }

        [TestMethod]
        public async Task BindingFromAttributeUse()
        {
            var (rsp, res) = await GuestClient.POSTAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(

                "api/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45/" +
                "?Bool=false&String=everything&XBlank=256",

                new()
                {
                    Bool = false,
                    DecimalNumber = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing",
                    Blank = 1
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("everything", res?.String);
            Assert.AreEqual(false, res?.Bool);
            Assert.AreEqual(99, res?.Int);
            Assert.AreEqual(483752874564876, res?.Long);
            Assert.AreEqual(2232.12, res?.Double);
            Assert.AreEqual("from body value", res?.FromBody);
            Assert.AreEqual(123.45m, res?.Decimal);
            Assert.AreEqual(256, res?.Blank);
        }

        [TestMethod]
        public async Task TestEventHandling()
        {
            var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
            var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };
            var event3 = new NewItemAddedToStock { ID = 3, Name = "three", Quantity = 30 };

            await Event<NewItemAddedToStock>.PublishAsync(event3, Mode.WaitForAll);
            await Event<NewItemAddedToStock>.PublishAsync(event2, Mode.WaitForAny);
            await Event<NewItemAddedToStock>.PublishAsync(event1, Mode.WaitForNone);

            Assert.AreEqual(0, event1.ID);
            Assert.AreEqual(0, event2.ID);
            Assert.AreEqual(0, event3.ID);

            Assert.AreEqual("pass", event1.Name);
            Assert.AreEqual("pass", event2.Name);
            Assert.AreEqual("pass", event3.Name);
        }

        [TestMethod]
        public async Task RangeHandling()
        {
            var res = await RangeClient.GetStringAsync("api/test-cases/range");
            Assert.AreEqual("fghij", res);
        }

        [TestMethod]
        public async Task FileHandling()
        {
            using var imageContent = new ByteArrayContent(
                await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            using var form = new MultipartFormDataContent
            {
                { imageContent, "File", "test.png" },
                { new StringContent("500"),"Width" },
                { new StringContent("500"),"Height" }
            };

            var res = await AdminClient.PostAsync("api/uploads/image/save", form);

            using var md5Instance = MD5.Create();
            using var stream = await res.Content.ReadAsStreamAsync();
            var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

            Assert.AreEqual("8A1F6A8E27D2E440280050DA549CBE3E", resMD5);
        }

        [TestMethod]
        public async Task FileHandlingFileBinding()
        {
            using var imageContent1 = new ByteArrayContent(
                await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
            imageContent1.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            using var imageContent2 = new ByteArrayContent(
                await new StreamContent(
                    File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());
            imageContent2.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            using var form = new MultipartFormDataContent
            {
                { imageContent1, "File1", "test.png" },
                { imageContent2, "File2", "test.png" },
                { new StringContent("500"),"Width" },
                { new StringContent("500"),"Height" }
            };

            var res = await AdminClient.PostAsync("/api/uploads/image/save-typed", form);

            using var md5Instance = MD5.Create();
            using var stream = await res.Content.ReadAsStreamAsync();
            var resMD5 = BitConverter.ToString(md5Instance.ComputeHash(stream)).Replace("-", "");

            Assert.AreEqual("8A1F6A8E27D2E440280050DA549CBE3E", resMD5);
        }

        [TestMethod]
        public async Task PreProcessorsAreRunIfValidationFailuresOccur()
        {
            var (rsp, res) = await AdminClient.POSTAsync<
                TestCases.PreProcessorIsRunOnValidationFailure.Endpoint,
                TestCases.PreProcessorIsRunOnValidationFailure.Request,
                ErrorResponse>
                (new()
                {
                    FailureCount = 0,
                    FirstName = ""
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, rsp.StatusCode);
            Assert.AreEqual(2, res.Errors.Count);
            Assert.AreEqual("blah", res.Errors["x"].First());
        }

        [TestMethod]
        public async Task OnBeforeOnAfterValidation()
        {
            var (rsp, res) = await AdminClient.POSTAsync<
                TestCases.OnBeforeAfterValidationTest.Endpoint,
                TestCases.OnBeforeAfterValidationTest.Request,
                TestCases.OnBeforeAfterValidationTest.Response>(new()
                {
                    Host = "blah",
                    Verb = Http.DELETE
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp.StatusCode);
            Assert.AreEqual("localhost", res.Host);
        }

        [TestMethod]
        public async Task PreProcessorShortCircuitMissingHeader()
        {
            var (rsp, res) = await GuestClient.GETAsync<
                Sales.Orders.Retrieve.Endpoint,
                Sales.Orders.Retrieve.Request,
                ErrorResponse>(new() { OrderID = "order1" });

            Assert.AreEqual(HttpStatusCode.BadRequest, rsp.StatusCode);
            Assert.AreEqual(1, res.Errors.Count);
            Assert.IsTrue(res.Errors.ContainsKey("MissingHeaders"));
        }

        [TestMethod]
        public async Task PreProcessorShortCircuitWrongHeaderValue()
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                var (rsp, res) = await AdminClient.POSTAsync<
                    Sales.Orders.Retrieve.Endpoint,
                    Sales.Orders.Retrieve.Request,
                    object>(new() { OrderID = "order1" });
            });
        }

        [TestMethod]
        public async Task PreProcessorShortCircuitHandlerExecuted()
        {
            var (rsp, res) = await CustomerClient.GETAsync<
                Sales.Orders.Retrieve.Endpoint,
                Sales.Orders.Retrieve.Request,
                ErrorResponse>(new() { OrderID = "order1" });

            Assert.AreEqual(HttpStatusCode.OK, rsp.StatusCode);
            Assert.AreEqual("ok!", res.Message);
        }

        [TestMethod]
        public async Task PlainTextBodyModelBinding()
        {
            using var stringContent = new StringContent("this is the body content");
            stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

            var rsp = await AdminClient.PostAsync("test-cases/plaintext/12345", stringContent);

            var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

            Assert.AreEqual("this is the body content", res.BodyContent);
            Assert.AreEqual(12345, res.Id);
        }

        [TestMethod]
        public async Task GlobalRoutePrefixOverride()
        {
            using var stringContent = new StringContent("this is the body content");
            stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

            var rsp = await AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent);

            var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

            Assert.AreEqual("this is the body content", res.BodyContent);
            Assert.AreEqual(12345, res.Id);
        }

        [TestMethod]
        public async Task GETRequestWithRouteParameterAndReqDto()
        {
            var (rsp, res) = await CustomerClient.GETAsync<EmptyRequest, ErrorResponse>(
                "/api/sales/orders/retrieve/54321",
                new());

            Assert.AreEqual(HttpStatusCode.OK, rsp.StatusCode);
            Assert.AreEqual("ok!", res.Message);
        }

        [TestMethod]
        public async Task QueryParamReadingInEndpointWithoutRequest()
        {
            var (rsp, res) = await GuestClient.GETAsync<
                EmptyRequest,
                TestCases.RouteBindingInEpWithoutReq.Response>(
                "/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=12", new());

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual(09809, res!.CustomerID);
            Assert.AreEqual(12, res!.OtherID);
        }

        [TestMethod]
        public async Task QueryParamReadingIsRequired()
        {
            var (rsp, res) = await GuestClient.GETAsync<
                EmptyRequest,
                ErrorResponse>(
                "/api/test-cases/ep-witout-req-query-param-binding-test?customerId=09809&otherId=lkjhlkjh", new());

            Assert.AreEqual(HttpStatusCode.BadRequest, rsp?.StatusCode);
            Assert.IsTrue(res?.Errors.ContainsKey("OtherID"));
        }
    }
}
