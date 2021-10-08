using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTests
{
    [TestClass]
    public class AdminUniTests
    {
        [TestMethod]
        public async Task AdminLoginWithBadInput()
        {
            var ep = new Admin.Login.Endpoint();
            await ep.TestAsync<Admin.Login.Validator>(new()
            {
                UserName = "x",
                Password = "y"
            });
            Assert.IsTrue(ep.ValidationFailed);
            Assert.AreEqual("Username too short!", ep.ValidationFailures[0].ErrorMessage);
            Assert.AreEqual("Password too short!", ep.ValidationFailures[1].ErrorMessage);
            Assert.AreEqual("Authentication Failed!", ep.ValidationFailures[2].ErrorMessage);
        }

        [TestMethod]
        public async Task AdminLoginInvalidCreds()
        {
            var ep = new Admin.Login.Endpoint();
            var res = await ep.TestAsync(new()
            {
                UserName = "admin",
                Password = "xxxxx"
            });

            Assert.IsTrue(ep.ValidationFailed);
            Assert.AreEqual("Authentication Failed!", ep.ValidationFailures[0].ErrorMessage);
        }
    }
}
