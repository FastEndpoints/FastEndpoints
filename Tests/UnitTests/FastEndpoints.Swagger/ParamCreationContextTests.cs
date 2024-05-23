// ReSharper disable ArrangeAttributes

using FastEndpoints.Swagger;
using Newtonsoft.Json;
using Xunit;

namespace ParamCreationContext;

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
        var operationPath = $"/route/{{{ParamName}:{paramType}}}/";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(expectedType);
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenNoTypeSpecified()
    {
        var operationPath = $"/route/{{{ParamName}}}/";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(string));
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenUnknownTypeIsSpecified()
    {
        var operationPath = $"/route/{{{ParamName}:unknownType}}/";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

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
        var operationPath = $"/route/{{{ParamName}:{paramType}}}/{{{OtherParam}:unknownType}}/";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

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
        var operationPath = $"/route/{{{ParamName}:{paramType}:min(10)}}:deactivate";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(expectedType);
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenTypeNotSpecified_AndGoogleRestApiGuidelineRouteStyle()
    {
        var operationPath = $"/route/{{{ParamName}}}:deactivate";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(string));
    }

    [Fact]
    public void ShouldBuildParamMapCorrectly_WhenTypeSpecified_AndHasMultipleConstraints_AndGoogleRestApiGuidelineRouteStyle()
    {
        var operationPath = $"/route/{{{ParamName}:min(5):max(10)}}:deactivate";
        var sut = new OperationProcessor.ParamCreationContext(null!, null!, null!, null, operationPath);

        sut.TypeForRouteParam(ParamName).Should().Be(typeof(long));
    }
}