using FastEndpoints.Swagger;
using Xunit;

namespace SchemaNameGen;

public class SchemaNameGeneratorTests
{
    static readonly SchemaNameGenerator _shortNameGenerator = new(shortSchemaNames: true);
    static readonly SchemaNameGenerator _longNameGenerator = new(shortSchemaNames: false);

    [Fact]
    public void ShortNameNonGeneric()
    {
        var res = _shortNameGenerator.Generate(typeof(Model));
        res.ShouldBe("Model");
    }

    [Fact]
    public void ShortNameGeneric()
    {
        var res = _shortNameGenerator.Generate(typeof(GenericModel<string>));
        res.ShouldBe("GenericModelOfString");
    }

    [Fact]
    public void ShortNameGenericDeep()
    {
        var res = _shortNameGenerator.Generate(typeof(GenericModel<GenericModel<List<Model>>>));
        res.ShouldBe("GenericModelOfGenericModelOfListOfModel");
    }

    [Fact]
    public void ShortNameGenericMulti()
    {
        var res = _shortNameGenerator.Generate(typeof(GenericMultiModel<List<Model>, GenericModel<int>>));
        res.ShouldBe("GenericMultiModelOfListOfModelAndGenericModelOfInt32");
    }

    [Fact]
    public void LongNameNonGeneric()
    {
        var res = _longNameGenerator.Generate(typeof(Model));
        res.ShouldBe("SchemaNameGenModel");
    }

    [Fact]
    public void LongNameGeneric()
    {
        var res = _longNameGenerator.Generate(typeof(GenericModel<string>));
        res.ShouldBe("SchemaNameGenGenericModelOfString");
    }

    [Fact]
    public void LongNameGenericDeep()
    {
        var res = _longNameGenerator.Generate(typeof(GenericModel<List<GenericModel<string>>>));
        res.ShouldBe("SchemaNameGenGenericModelOfListOfGenericModelOfString");
    }

    [Fact]
    public void LongNameGenericDeepMulti()
    {
        var res = _longNameGenerator.Generate(typeof(GenericMultiModel<List<GenericModel<string>>, GenericMultiModel<int, string>>));
        res.ShouldBe("SchemaNameGenGenericMultiModelOfListOfGenericModelOfStringAndGenericMultiModelOfInt32AndString");
    }
}

public class Model { }

public class GenericModel<T> { }

public class GenericMultiModel<T1, T2> { }