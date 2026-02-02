using System.Net.Http.Json;
using NativeAotChecker.Endpoints;

namespace NativeAotCheckerTests;

public class NullableBoolQueryBindingTest(App app) : IClassFixture<App>
{
    /// <summary>
    /// Tests nullable bool (bool?) query parameter binding in AOT mode.
    /// 
    /// AOT ISSUE: Nullable value type binding from query string fails in Native AOT.
    /// - Non-nullable bool query param works: ?NonNullableBool=true → 200 OK
    /// - Nullable bool? query param fails: ?NullableBool=true → 500 Error
    /// 
    /// The issue is with parsing nullable value types from query strings in AOT mode.
    /// </summary>
    [Fact]
    public async Task Nullable_Bool_Query_Parameter_Binding_Fails_In_AOT_Mode()
    {
        // This works - non-nullable bool
        var rsp1 = await app.Client.GetAsync("nullable-bool-query-test?NonNullableBool=true");
        rsp1.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // This fails in AOT - nullable bool?
        var rsp2 = await app.Client.GetAsync("nullable-bool-query-test?NonNullableBool=true&NullableBool=true");
        
        if (!rsp2.IsSuccessStatusCode)
            Assert.Fail($"Nullable bool query binding failed with status {rsp2.StatusCode}. " +
                        $"Non-nullable bool works, but bool? fails in AOT mode.");

        var res = await rsp2.Content.ReadFromJsonAsync<NullableBoolQueryResponse>();
        res.ShouldNotBeNull();
        res.NonNullableBool.ShouldBeTrue();
        res.NullableBool.ShouldBe(true);
    }
}
