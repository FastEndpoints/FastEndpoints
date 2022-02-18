using Microsoft.VisualStudio.TestTools.UnitTesting;
using FastEndpoints.Swagger;

namespace Test
{
    [TestClass]
    public class SwaggerTests
    {
        //private static defautsche
    }

    public class GenericModel<T>
    {
        public T[]? Values { get; set; }
    }

    public class Model
    {
        public string? Name { get; set; }
    }
}
