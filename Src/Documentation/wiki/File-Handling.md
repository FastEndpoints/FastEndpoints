# handling file uploads

the following example relays back the image data uploaded to the endpoint in order to demonstrate both receiving and sending of file data:

```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/api/uploads/image");
        AllowFileUploads();
    }

    public override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        if (Files.Count > 0)
        {
            var file = Files[0];

            await SendStreamAsync(
                stream: file.OpenReadStream(),
                fileName: "test.png",
                fileLengthBytes: file.Length,
                contentType: "image/png");

            return;
        }
        await SendNoContentAsync();
    }
}
```

endpoints by default won't allow `multipart/form-data` content uploads. you'd have to enable file uploads by using the `AllowFileUploads()` method in the handler configuration like shown above. the received files are exposed to the endpoint handler via the `Files` property which is of `IFormFileCollection` type.

## binding files to dto
file data can also be automatically bound to the request dto by simply adding an `IFormFile` property with a matching name.
```csharp
public class MyRequest
{
    public int Width { get; set; }
    public int Height { get; set; }
    public IFormFile File1 { get; set; }
    public IFormFile File2 { get; set; }
    public IFormFile File3 { get; set; }
}
```

## handling large file uploads without buffering
in asp.net, accessing `IFormFileCollection` or `IFormFile` causes the complete uploaded file to be read from the request stream and buffered to either memory or disk. you can avoid this buffering and reduce server resource utilization by manually reading the multipart file sections with the combination of `AllowFileUploads(dontAutoBindFormData: true)` and `FormFileSectionsAsync()` methods as shown below:

```csharp
public class Upload : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/file-upload");
        AllowFileUploads(dontAutoBindFormData: true); //turns off buffering
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await foreach (var section in FormFileSectionsAsync(ct))
        {
            if (section is not null)
            {
                using (var fs = System.IO.File.Create(section.FileName))
                {
                    await section.Section.Body.CopyToAsync(fs, 1024 * 64, ct);
                }
            }
        }

        await SendOkAsync("upload complete!");
    }
}
```

you may also need to increase the max request body size in kestrel to allow large file uploads like so:
```csharp
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 1073741824; //set to max allowed file size of your system
});
```

# sending file responses

there are 3 methods you can use to send file data down to the client.

**SendStreamAsync()** - supply a `System.IO.Stream` to this method for reading binary data from.

**SendFileAsync()** - supply a `System.IO.FileInfo` instance as the source of the binary data.

**SendBytesAsync()** - supply a byte array as the source of data to be sent to the client.

all three methods allow you to optionally specify the `content-type` and `file name`. if file name is specified, the `Content-Disposition: attachment` response header will be set with the given file name so that a file download will be initiated by the client/browser. range requests/ partial responses are also supported by setting the `enableRangeProcessing` parameter to `true`.

# write to response stream

instead of using the above methods, you also have the choice of writing directly to the http response stream.
> **[see here](https://github.com/dj-nitehawk/FastEndpoints-FileHandling-Demo)** for an example project that stores and retrieves images in mongodb.