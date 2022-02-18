using FastEndpoints.Swagger;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class SwaggerTests
    {
        private static readonly SchemaNameGenerator shortNameGenerator = new(shortSchemaNames: true);
        private static readonly SchemaNameGenerator longNameGenerator = new(shortSchemaNames: false);

        [TestMethod]
        public void ShortNameNonGeneric()
        {
            var res = shortNameGenerator.Generate(typeof(Model));
            Assert.AreEqual("Model", res);
        }

        [TestMethod]
        public void ShortNameGeneric()
        {
            var res = shortNameGenerator.Generate(typeof(GenericModel<string>));
            Assert.AreEqual("GenericModelOfString", res);
        }

        [TestMethod]
        public void ShortNameGenericDeep()
        {
            var res = shortNameGenerator.Generate(typeof(GenericModel<GenericModel<List<Model>>>));
            Assert.AreEqual("GenericModelOfGenericModelOfListOfModel", res);
        }

        [TestMethod]
        public void ShortNameGenericMulti()
        {
            var res = shortNameGenerator.Generate(typeof(GenericMultiModel<List<Model>, GenericModel<int>>));
            Assert.AreEqual("GenericMultiModelOfListOfModelAndGenericModelOfInt32", res);
        }

        [TestMethod]
        public void LongNameNonGeneric()
        {
            var res = longNameGenerator.Generate(typeof(Model));
            Assert.AreEqual("TestModel", res);
        }

        [TestMethod]
        public void LongNameGeneric()
        {
            var res = longNameGenerator.Generate(typeof(GenericModel<string>));
            Assert.AreEqual("TestGenericModelOfString", res);
        }

        [TestMethod]
        public void LongNameGenericDeep()
        {
            var res = longNameGenerator.Generate(typeof(GenericModel<List<GenericModel<string>>>));
            Assert.AreEqual("TestGenericModelOfListOfGenericModelOfString", res);
        }

        [TestMethod]
        public void LongNameGenericDeepMulti()
        {
            var res = longNameGenerator.Generate(typeof(GenericMultiModel<List<GenericModel<string>>, GenericMultiModel<int, string>>));
            Assert.AreEqual("TestGenericMultiModelOfListOfGenericModelOfStringAndGenericMultiModelOfInt32AndString", res);
        }
    }

    public class Model { }
    public class GenericModel<T> { }
    public class GenericMultiModel<T1, T2> { }
}
