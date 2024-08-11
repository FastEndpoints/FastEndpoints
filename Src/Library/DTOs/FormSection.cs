using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.WebUtilities;

namespace FastEndpoints;

/// <summary>
/// represents a multipart form section which could contain either a <see cref="FormMultipartSection" /> or a <see cref="FileMultipartSection" />
/// </summary>
/// <param name="form"></param>
/// <param name="file"></param>
public readonly struct MultipartSection(FormMultipartSection? form, FileMultipartSection? file)
{
    public FormMultipartSection? FormSection { get; } = form;

    [MemberNotNullWhen(true, nameof(FormSection))]
    public bool IsFormSection => FormSection is not null;

    public FileMultipartSection? FileSection { get; } = file;

    [MemberNotNullWhen(true, nameof(FileSection))]
    public bool IsFileSection => FileSection is not null;
}