namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents the current message with its properties.
/// Contains message text, data, and context information.
/// </summary>
public class CurrentMessage
{
    private IReadOnlyList<UploadedFile>? _files;

    /// <summary>Gets the text content of the message.</summary>
    public string Text { get; }

    /// <summary>The participant ID for this message context.</summary>
    public string ParticipantId { get; }

    /// <summary>The request ID for this message context.</summary>
    public string RequestId { get; }

    /// <summary>The scope for this message context, if any.</summary>
    public string? Scope { get; }

    /// <summary>The hint for this message context, if any.</summary>
    public string? Hint { get; }

    /// <summary>The authorization token for this message context, if any.</summary>
    public string? Authorization { get; }

    /// <summary>The thread ID for this message context, if any.</summary>
    public string? ThreadId { get; }

    /// <summary>The data associated with this message context, if any.</summary>
    public object? Data { get; }

    /// <summary>The tenant ID for this message context.</summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the files decoded from the message data, typed as <see cref="UploadedFile"/> objects.
    /// Primarily for File messages (type="File") handled by OnFileUpload handlers.
    /// Supports the multi-file format <c>{ files: [{ content, fileName, contentType, fileSize }, ...] }</c>,
    /// the single-file object format <c>{ content, fileName, contentType }</c>,
    /// and a raw base64 string (in which case <see cref="Text"/> is used as the file name).
    /// Returns an empty list when the data contains no recognizable file content.
    /// </summary>
    public IReadOnlyList<UploadedFile> Files =>
        _files ??= FileUploadParser.Parse(Data, string.IsNullOrEmpty(Text) ? null : Text);

    internal CurrentMessage(
        string text,
        string participantId,
        string requestId,
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string? authorization = null,
        string? threadId = null)
    {
        Text = text;
        ParticipantId = participantId;
        RequestId = requestId;
        Scope = scope;
        Hint = hint;
        Data = data;
        TenantId = tenantId;
        Authorization = authorization;
        ThreadId = threadId;
    }
}
