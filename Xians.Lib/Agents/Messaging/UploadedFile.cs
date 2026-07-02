namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents a single file received via a File message (type="File").
/// Provides typed access to the base64 content and optional metadata,
/// removing the need for manual JSON parsing in OnFileUpload handlers.
/// </summary>
public class UploadedFile
{
    /// <summary>
    /// The base64 encoded file content. Empty for reference-only files (those carrying a
    /// <see cref="FileId"/>) until the bytes are resolved from server storage.
    /// </summary>
    public string Content { get; internal set; }

    /// <summary>The file name, if provided by the client.</summary>
    public string? FileName { get; }

    /// <summary>The MIME content type (e.g. "application/pdf"), if provided by the client.</summary>
    public string? ContentType { get; }

    /// <summary>The file size in bytes, if provided by the client.</summary>
    public long? FileSize { get; }

    /// <summary>
    /// The server storage id for files stored out-of-band (GridFS). When set, the content is
    /// downloaded on demand and populated into <see cref="Content"/> before the handler runs.
    /// </summary>
    public string? FileId { get; }

    /// <summary>True when this file carries only a reference and its bytes are not yet resolved.</summary>
    public bool IsReference => !string.IsNullOrEmpty(FileId) && string.IsNullOrEmpty(Content);

    public UploadedFile(string content, string? fileName = null, string? contentType = null, long? fileSize = null)
        : this(content, fileName, contentType, fileSize, fileId: null)
    {
    }

    public UploadedFile(string? content, string? fileName, string? contentType, long? fileSize, string? fileId)
    {
        // Content may be empty for reference-only files, but a file must have either content or a fileId.
        if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(fileId))
        {
            throw new ArgumentException("An uploaded file must have either content or a fileId.", nameof(content));
        }
        Content = content ?? string.Empty;
        FileName = fileName;
        ContentType = contentType;
        FileSize = fileSize;
        FileId = fileId;
    }

    /// <summary>
    /// Decodes the base64 content into raw bytes.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the content is not valid base64.</exception>
    public byte[] GetBytes() => Convert.FromBase64String(Content);

    /// <summary>
    /// Attempts to decode the base64 content into raw bytes without throwing.
    /// </summary>
    /// <param name="bytes">The decoded bytes, or null when the content is not valid base64.</param>
    /// <returns>True when decoding succeeded.</returns>
    public bool TryGetBytes(out byte[]? bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(Content);
            return true;
        }
        catch (FormatException)
        {
            bytes = null;
            return false;
        }
    }
}
