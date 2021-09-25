using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class MiscTestCases
    {
        [TestMethod]
        public async Task ClaimMissing()
        {
            var (_, result) = await AdminClient.PostAsync<TestCases.MissingClaimTest.ThrowIfMissingRequest, ErrorResponse>(
                "/test-cases/missing-claim-test",
                new()
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
            var (res, result) = await AdminClient.PostAsync<TestCases.MissingClaimTest.DontThrowIfMissingRequest, string>(
                "/test-cases/missing-claim-test/dont-throw",
                new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.AreEqual("you sent xyz", result);
        }
    }
}
