using NativeAotChecker.Endpoints.Mapping;

namespace NativeAotCheckerTests;

public class MappingTests(App app)
{
    [Fact]
    public async Task Mapper_Is_Detected_And_Used_Correctly()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<MapperTestEndpoint, MapperTestRequest, MapperTestResponse>(
                                  new()
                                  {
                                      FirstName = "John",
                                      LastName = "Doe",
                                      Age = 30
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FullName.ShouldBe("John Doe");
        res.Age.ShouldBe(30);
        res.MapperWasUsed.ShouldBeTrue();
    }
}
