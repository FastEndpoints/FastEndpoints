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
    }
}
