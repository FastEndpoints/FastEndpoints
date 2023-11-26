namespace Swagger;

[UsesVerify]
public class SwaggerDocTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task initial_release_doc_produces_correct_output()
    {
        var doc = await Fixture.DocGenerator.GenerateAsync("Initial Release");
        await VerifyJson(doc.ToJson());
    }

    [Fact]
    public async Task release_1_doc_produces_correct_output()
    {
        var doc = await Fixture.DocGenerator.GenerateAsync("Release 1.0");
        await VerifyJson(doc.ToJson());
    }

    [Fact]
    public async Task release_2_doc_produces_correct_output()
    {
        var doc = await Fixture.DocGenerator.GenerateAsync("Release 2.0");
        await VerifyJson(doc.ToJson());
    }
}