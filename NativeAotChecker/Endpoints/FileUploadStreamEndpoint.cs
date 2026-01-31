using FastEndpoints;
using Microsoft.AspNetCore.Http.Features;

namespace NativeAotChecker.Endpoints;

// Response DTO
public class FileUploadStreamResponse
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SectionCount { get; set; }
    public bool StreamingWorked { get; set; }
    public List<string> ContentTypes { get; set; } = [];
}

/// <summary>
/// Tests FormFileSectionsAsync() for streaming file uploads in AOT mode.
/// AOT ISSUE: FormFileSectionsAsync uses IFormFeature which may use reflection.
/// Multipart form parsing uses runtime type dispatch for section handling.
/// IAsyncEnumerable<FileMultipartSection> iteration needs async state machine.
/// </summary>
public class FileUploadStreamEndpoint : EndpointWithoutRequest<FileUploadStreamResponse>
{
    public override void Configure()
    {
        Post("file-upload-stream");
        AllowAnonymous();
        AllowFileUploads(dontAutoBindFormData: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sections = new List<(string name, string contentType, long size)>();
        
        await foreach (var section in FormFileSectionsAsync(ct))
        {
            if (section is not null)
            {
                // Read the stream to get the size
                using var ms = new MemoryStream();
                await section.FileStream!.CopyToAsync(ms, ct);
                sections.Add((section.Name ?? "unknown", section.Section.ContentType ?? "unknown", ms.Length));
            }
        }

        await Send.OkAsync(new FileUploadStreamResponse
        {
            FileName = sections.FirstOrDefault().name ?? "none",
            FileSize = sections.Sum(s => s.size),
            SectionCount = sections.Count,
            StreamingWorked = sections.Count > 0,
            ContentTypes = sections.Select(s => s.contentType).ToList()
        });
    }
}
