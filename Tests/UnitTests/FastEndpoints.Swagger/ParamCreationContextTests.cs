using FastEndpoints.Swagger;
using Newtonsoft.Json;
using Xunit;

namespace SchemaNameGen;

public class ParamCreationContextTests
{
    const string ParamName = "param";
    const string OtherParam = "otherParam";

    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("bool", typeof(bool))]
    [InlineData("datetime", typeof(DateTime))]
    [InlineData("decimal", typeof(decimal))]
    [InlineData("double", typeof(double))]
    [InlineData("float", typeof(float))]
    [InlineData("guid", typeof(Guid))]
    [InlineData("long", typeof(long))]
    [InlineData("min", typeof(long))]
    [InlineData("max", typeof(long))]
    [InlineData("range", typeof(long))]
    public void ShouldBuildParamMapCorrectly_WhenKnownTypeIsSpecified(string paramType, Type expectedType)
    {
        string operationPath = $"/route/{{{ParamName}:{paramType}}}/";
        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(expectedType);
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenNoTypeSpecified()
    {
        string operationPath = $"/route/{{{ParamName}}}/";
        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(string));
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenUnknownTypeIsSpecified()
    {
        string operationPath = $"/route/{{{ParamName}:unknownType}}/";
        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(string));
    }

    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("bool", typeof(bool))]
    [InlineData("datetime", typeof(DateTime))]
    [InlineData("decimal", typeof(decimal))]
    [InlineData("double", typeof(double))]
    [InlineData("float", typeof(float))]
    [InlineData("guid", typeof(Guid))]
    [InlineData("long", typeof(long))]
    [InlineData("min", typeof(long))]
    [InlineData("max", typeof(long))]
    [InlineData("range", typeof(long))]
    public void ShouldBuildParamMapCorrectly_WhenMixedParamTypesSpecified(string paramType, Type expectedType)
    {
        string operationPath = $"/route/{{{ParamName}:{paramType}}}/{{{OtherParam}:unknownType}}/";

        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(expectedType);
        sut.TypeForRouteParam(OtherParam).Should().Be(typeof(string));
    }

    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("bool", typeof(bool))]
    [InlineData("datetime", typeof(DateTime))]
    [InlineData("decimal", typeof(decimal))]
    [InlineData("double", typeof(double))]
    [InlineData("float", typeof(float))]
    [InlineData("guid", typeof(Guid))]
    [InlineData("long", typeof(long))]
    [InlineData("min", typeof(long))]
    [InlineData("max", typeof(long))]
    [InlineData("range", typeof(long))]
    [InlineData("unknownType", typeof(string))]
    public void ShouldBuildParamMapCorrectly_WhenTypeIsSpecified_AndGoogleRestApiGuidelineRouteStyle(string paramType, Type expectedType)
    {
        string operationPath = $"/route/{{{ParamName}:{paramType}}}:deactivate";

        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(expectedType);
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenTypeNotSpecified_AndGoogleRestApiGuidelineRouteStyle()
    {
        string operationPath = $"/route/{{{ParamName}}}:deactivate";

        OperationProcessor.ParamCreationContext sut = new OperationProcessor.ParamCreationContext(
            null,
            null,
            JsonSerializer.CreateDefault(),
            null,
            operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(string));
    }
}