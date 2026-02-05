using System.Net.Http.Json;
using NativeAotChecker.Endpoints.KnownAotIssues;

namespace NativeAotCheckerTests;

public class StructDtoTest(App app)
{
    /// <summary>
    /// Tests struct types as request DTOs in AOT mode.
    /// AOT ISSUE: Struct binding requires boxing/unboxing which needs type metadata.
    /// Value type instantiation differs from reference types in AOT.
    /// Struct default values and parameterless constructor handling use reflection.
    /// </summary>
    [Fact] // AOT ISSUE: Struct types as request DTOs may not work in Native AOT mode
    public async Task Struct_Types_Work_In_AOT_Mode()
    {
        var req = new StructRequest
        {
            Id = 123,
            Name = "Test Struct",
            Value = 99.99
        };

        var (rsp, res, err) = await app.Client.POSTAsync<StructTypesEndpoint,
StructRequest, StructResponse>(req);

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.Id.ShouldBe(123);
        res.Name.ShouldBe("Test Struct");
        res.Value.ShouldBe(99.99);
        res.IsValid.ShouldBeTrue();
    }
}
