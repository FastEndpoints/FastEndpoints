using FastEndpoints.Swagger;
using Xunit;

namespace SchemaNameGen;

public class SchemaNameGeneratorTests
{
    private static readonly SchemaNameGenerator shortNameGenerator = new(shortSchemaNames: true);
    private static readonly SchemaNameGenerator longNameGenerator = new(shortSchemaNames: false);

    [Fact]
    public void ShortNameNonGeneric()
    {
        var res = shortNameGenerator.Generate(typeof(Model));
        res.Should().Be("Model");
    }

    [Fact]
    public void ShortNameGeneric()
    {
        var res = shortNameGenerator.Generate(typeof(GenericModel<string>));
        res.Should().Be("GenericModelOfString");
    }

    [Fact]
    public void ShortNameGenericDeep()
    {
        var res = shortNameGenerator.Generate(typeof(GenericModel<GenericModel<List<Model>>>));
        res.Should().Be("GenericModelOfGenericModelOfListOfModel");
    }

    [Fact]
    public void ShortNameGenericMulti()
    {
        var res = shortNameGenerator.Generate(typeof(GenericMultiModel<List<Model>, GenericModel<int>>));
        res.Should().Be("GenericMultiModelOfListOfModelAndGenericModelOfInt32");
    }

    [Fact]
    public void LongNameNonGeneric()
    {
        var res = longNameGenerator.Generate(typeof(Model));
        res.Should().Be("SchemaNameGenModel");
    }

    [Fact]
    public void LongNameGeneric()
    {
        var res = longNameGenerator.Generate(typeof(GenericModel<string>));
        res.Should().Be("SchemaNameGenGenericModelOfString");
    }

    [Fact]
    public void LongNameGenericDeep()
    {
        var res = longNameGenerator.Generate(typeof(GenericModel<List<GenericModel<string>>>));
        res.Should().Be("SchemaNameGenGenericModelOfListOfGenericModelOfString");
    }

    [Fact]
    public void LongNameGenericDeepMulti()
    {
        var res = longNameGenerator.Generate(typeof(GenericMultiModel<List<GenericModel<string>>, GenericMultiModel<int, string>>));
        res.Should().Be("SchemaNameGenGenericMultiModelOfListOfGenericModelOfStringAndGenericMultiModelOfInt32AndString");
    }
}

public class Model { }
public class GenericModel<T> { }
public class GenericMultiModel<T1, T2> { }