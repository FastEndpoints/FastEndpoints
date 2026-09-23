using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialPayloadHashTests
{
    [Fact]
    public async Task Same_Json_Body_Is_Stable()
    {
        var a = await Hash(body: """{"a":1}"""u8.ToArray());
        var b = await Hash(body: """{"a":1}"""u8.ToArray());

        a.ShouldBe(b);
    }

    [Theory]
    [InlineData("application/json", "application/json; charset=utf-16")]
    [InlineData("application/json", "text/plain")]
    [InlineData("application/x-www-form-urlencoded; charset=utf-8", "application/x-www-form-urlencoded; charset=utf-16")]
    public async Task Different_Declared_Representation_Changes_Hash(string firstType, string secondType)
    {
        var bytes = "value=10"u8.ToArray();
        (await HashWithType(firstType)).ShouldNotBe(await HashWithType(secondType));

        async Task<byte[]> HashWithType(string contentType)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.ContentType = contentType;
            ctx.Request.Body = new MemoryStream(bytes);
            return await FinancialPayloadHash.ComputeAsync(ctx.Request, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Equivalent_Representation_Spelling_Hashes_The_Same()
    {
        var bytes = "{\"value\":10}"u8.ToArray();
        (await HashWithType("Application/JSON; charset=\"UTF-8\"")).ShouldBe(await HashWithType("application/json; charset=utf-8"));

        async Task<byte[]> HashWithType(string contentType)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.ContentType = contentType;
            ctx.Request.Body = new MemoryStream(bytes);
            return await FinancialPayloadHash.ComputeAsync(ctx.Request, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Query_Order_Does_Not_Change_Hash()
    {
        var a = await Hash(query: "?b=2&a=1");
        var b = await Hash(query: "?a=1&b=2");

        a.ShouldBe(b);
    }

    [Fact]
    public async Task Query_Change_Changes_Hash()
    {
        var a = await Hash(query: "?x=1");
        var b = await Hash(query: "?x=2");

        a.ShouldNotBe(b);
    }

    [Fact]
    public async Task Query_And_Body_Concat_Does_Not_Collide()
    {
        var a = await Hash(query: "?x=1", body: "00"u8.ToArray());
        var b = await Hash(query: "?x=10", body: "0"u8.ToArray());

        a.ShouldNotBe(b);
    }

    [Fact]
    public async Task Form_Concat_Does_Not_Collide()
    {
        var a = await Hash(form: Form(("a", "bc")));
        var b = await Hash(form: Form(("ab", "c")));

        a.ShouldNotBe(b);
    }

    [Fact]
    public async Task Form_Multi_Value_Does_Not_Collide_With_Comma()
    {
        var comma = await Hash(form: Form(("a", "1,2")));
        var multi = await Hash(form: new FormCollection(new Dictionary<string, StringValues> { ["a"] = new(["1", "2"]) }));

        comma.ShouldNotBe(multi);
    }

    [Fact]
    public async Task Form_Field_Order_Does_Not_Change_Hash()
    {
        var a = await Hash(form: Form(("a", "1"), ("b", "2")));
        var b = await Hash(form: Form(("b", "2"), ("a", "1")));

        a.ShouldBe(b);
    }

    [Fact]
    public async Task Form_Files_Include_Metadata()
    {
        var a = await Hash(form: FormWithFile("file", "a.bin", 10));
        var b = await Hash(form: FormWithFile("file", "a.bin", 10));
        var c = await Hash(form: FormWithFile("file", "b.bin", 10));

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact]
    public async Task Actual_File_Bytes_Change_Hash()
    {
        FormCollection With(byte value) => new(new Dictionary<string, StringValues>(), new FormFileCollection
        {
            new FormFile(new MemoryStream(new byte[] { value }), 0, 1, "file", "same.bin") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" }
        });
        (await Hash(form: With(1))).ShouldNotBe(await Hash(form: With(2)));
    }

    [Fact]
    public async Task Hashing_Form_Files_Does_Not_Prevent_Later_Reads()
    {
        var bytes = "upload-bytes"u8.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "a.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
        var form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });
        await Hash(form: form);

        await using var again = file.OpenReadStream();
        var buffer = new byte[bytes.Length];
        (await again.ReadAsync(buffer)).ShouldBe(bytes.Length);
        buffer.ShouldBe(bytes);
    }

    [Fact]
    public async Task Buffering_State_Does_Not_Change_Hash_And_Position_Is_Restored()
    {
        var bytes = "same payload"u8.ToArray();
        var expected = await Hash(body: bytes);
        var context = new DefaultHttpContext();
        context.Request.Body = new NonSeekable(bytes);
        context.Request.ContentType = "application/json";
        context.Request.EnableBuffering();
        await context.Request.Body.ReadExactlyAsync(new byte[3]);
        (await FinancialPayloadHash.ComputeAsync(context.Request, default)).ShouldBe(expected);
        context.Request.Body.Position.ShouldBe(3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await FinancialPayloadHash.ComputeAsync(context.Request, cancellation.Token));
        context.Request.Body.Position.ShouldBe(3);
    }

    [Fact]
    public async Task Raw_Payload_Preserves_Version_One_Encoding()
    {
        var hash = await Hash(query: "?b=2&a=1", body: """{"a":1}"""u8.ToArray());

        Convert.ToHexString(hash).ShouldBe("45B287D260BFB55762A947B0CCA35884BA4BCD5418579AFA2A444CF3D13F9E0A");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Encoded_Form_Preserves_Encoding_And_Restores_Position(bool buffered)
    {
        var context = new DefaultHttpContext();
        var bytes = "b=hello+world&a=1&a=2"u8.ToArray();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.QueryString = new("?b=2&a=1");
        context.Request.Body = buffered ? new NonSeekable(bytes) : new MemoryStream(bytes);
        context.Request.EnableBuffering();
        await context.Request.Body.ReadExactlyAsync(new byte[3]);

        var hash = await FinancialPayloadHash.ComputeAsync(context.Request, default);

        Convert.ToHexString(hash).ShouldBe("EE6314D32D5AF13B84842857060C2FA1D90C86A441734AC06C0C2F9F662C2408");
        context.Request.Body.Position.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Encoded_Form_Parsing_Failure_Restores_Position(bool buffered)
    {
        var context = new DefaultHttpContext();
        var bytes = "a=1&b=2"u8.ToArray();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = buffered ? new NonSeekable(bytes) : new MemoryStream(bytes);
        context.Request.EnableBuffering();
        await context.Request.Body.ReadExactlyAsync(new byte[3]);
        context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions { ValueCountLimit = 1 }));

        await Should.ThrowAsync<InvalidDataException>(async () => await FinancialPayloadHash.ComputeAsync(context.Request, default));

        context.Request.Body.Position.ShouldBe(3);
    }

    sealed class NonSeekable(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    static async Task<byte[]> Hash(string? query = null, byte[]? body = null, IFormCollection? form = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";

        if (query is not null)
            ctx.Request.QueryString = new(query.StartsWith('?') ? query : "?" + query);

        if (form is not null)
        {
            ctx.Request.ContentType = "application/x-www-form-urlencoded";
            ctx.Features.Set<IFormFeature>(new FormFeature(form));
        }
        else
        {
            var bytes = body ?? [];
            ctx.Request.ContentType = "application/json";
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
        }

        return await FinancialPayloadHash.ComputeAsync(ctx.Request, CancellationToken.None);
    }

    static FormCollection Form(params (string Key, string Value)[] fields)
        => new(fields.ToDictionary(f => f.Key, f => (StringValues)f.Value, StringComparer.Ordinal));

    static FormCollection FormWithFile(string name, string fileName, long length)
    {
        var files = new FormFileCollection { new DummyFile(name, fileName, length) };

        return new(new Dictionary<string, StringValues>(), files);
    }

    sealed class DummyFile(string name, string fileName, long length) : IFormFile
    {
        public string ContentType => "application/octet-stream";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; } = length;
        public string Name { get; } = name;
        public string FileName { get; } = fileName;
        public Stream OpenReadStream() => Stream.Null;
        public void CopyTo(Stream target) { }
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
