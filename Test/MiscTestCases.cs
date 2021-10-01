using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using TestCases.EventHandlingTest;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class MiscTestCases
    {
        [TestMethod]
        public async Task ClaimMissing()
        {
            var (_, result) = await AdminClient.PostAsync<
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
            var (res, result) = await AdminClient.PostAsync<
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
        public async Task RouteValueBinding()
        {
            var (rsp, res) = await GuestClient.PostAsync<TestCases.RouteBindingTest.Request, TestCases.RouteBindingTest.Response>(

                "/test-cases/route-binding-test/something/true/99/483752874564876/2232.12/123.45",

                new()
                {
                    Bool = false,
                    Decimal = 1,
                    Double = 1,
                    FromBody = "from body value",
                    Int = 1,
                    Long = 1,
                    String = "nothing"
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("something", res?.String);
            Assert.AreEqual(true, res?.Bool);
            Assert.AreEqual(99, res?.Int);
            Assert.AreEqual(483752874564876, res?.Long);
            Assert.AreEqual(2232.12, res?.Double);
            Assert.AreEqual("from body value", res?.FromBody);
            Assert.AreEqual(123.45m, res?.Decimal);
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
    }
}
