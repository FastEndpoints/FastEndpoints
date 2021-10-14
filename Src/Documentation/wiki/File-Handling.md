# handling file uploads

the following example relays back the image data uploaded to the endpoint in order to demonstrate both receiving and sending of file data:

```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public MyEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/uploads/image");
        AllowFileUploads();
    }

    protected override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        if (Files.Count > 0)
        {
            var file = Files[0];

            await SendStreamAsync(
                stream: file.OpenReadStream(),
                fileName: "test.png",
                fileLengthBytes: file.Length,
                contentType: "image/png");
        }
        await SendNoContentAsync();
    }
}
```

endpoints by default won't allow `multipart/form-data` content uploads. you'd have to enable file uploads by using the `AllowFileUploads()` method in the handler constructor like shown above. the received files are exposed to the endpoint handler via the `Files` property which is of `IFormFileCollection` type.

# sending file responses

there are 3 methods you can use to send file data down to the client.

**SendStreamAsync()** - you can supply a `System.IO.Stream` to this method for reading binary data from.

**SendFileAsync()** - you can supply a `System.IO.FileInfo` instance as the source of the binary data.

**SendBytesAsync()** - you can supply a byte array as the source of data to be sent to the client.

all three methods allow you to optionally specify the `content-type` and `file name`. if file name is specified, the `Content-Disposition: attachment` response header will be set with the given file name so that a file download will be initiated by the client/browser.