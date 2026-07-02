using System.Text.Json;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Decodes file upload payloads from message data into typed <see cref="UploadedFile"/> objects.
/// Supports all wire formats used by File messages (type="File"):
/// <list type="bullet">
/// <item>Multi-file: <c>{ "files": [{ "content", "fileName", "contentType", "fileSize" }, ...] }</c></item>
/// <item>Single object: <c>{ "content", "fileName", "contentType", "fileSize" }</c></item>
/// <item>Raw base64 string: <c>"JVBERi0x..."</c></item>
/// </list>
/// </summary>
internal static class FileUploadParser
{
    /// <summary>
    /// Parses message data into a list of uploaded files.
    /// Returns an empty list when the data is null or contains no recognizable file content.
    /// </summary>
    /// <param name="data">The message data (JsonElement, string, or serializable object).</param>
    /// <param name="fallbackFileName">Optional file name to use for raw base64 payloads (typically the message text).</param>
    public static IReadOnlyList<UploadedFile> Parse(object? data, string? fallbackFileName = null)
    {
        if (data == null)
        {
            return Array.Empty<UploadedFile>();
        }

        var element = ToJsonElement(data);
        if (element == null)
        {
            // Non-JSON data (e.g. a plain string): treat as raw base64 content
            var raw = data.ToString();
            return string.IsNullOrEmpty(raw)
                ? Array.Empty<UploadedFile>()
                : new[] { new UploadedFile(raw, fallbackFileName) };
        }

        return Parse(element.Value, fallbackFileName);
    }

    private static IReadOnlyList<UploadedFile> Parse(JsonElement element, string? fallbackFileName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var content = element.GetString();
                return string.IsNullOrEmpty(content)
                    ? Array.Empty<UploadedFile>()
                    : new[] { new UploadedFile(content, fallbackFileName) };

            case JsonValueKind.Array:
                return ParseFileArray(element);

            case JsonValueKind.Object:
                // Multi-file format: { files: [...] }
                if (TryGetPropertyIgnoreCase(element, "files", out var filesProp) &&
                    filesProp.ValueKind == JsonValueKind.Array)
                {
                    return ParseFileArray(filesProp);
                }

                // Single file object format: { content, fileName, ... }
                var single = ParseFileObject(element);
                return single != null ? new[] { single } : Array.Empty<UploadedFile>();

            default:
                return Array.Empty<UploadedFile>();
        }
    }

    private static IReadOnlyList<UploadedFile> ParseFileArray(JsonElement array)
    {
        var files = new List<UploadedFile>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var file = ParseFileObject(item);
                if (file != null)
                {
                    files.Add(file);
                }
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var content = item.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    files.Add(new UploadedFile(content));
                }
            }
        }
        return files;
    }

    private static UploadedFile? ParseFileObject(JsonElement obj)
    {
        // Inline content (base64) is optional: reference-only files carry a "fileId" instead,
        // and their bytes are resolved from server storage before the handler runs.
        string? content = null;
        if (TryGetPropertyIgnoreCase(obj, "content", out var contentProp) &&
            contentProp.ValueKind == JsonValueKind.String)
        {
            content = contentProp.GetString();
        }

        string? fileId = null;
        if (TryGetPropertyIgnoreCase(obj, "fileId", out var fid) && fid.ValueKind == JsonValueKind.String)
        {
            fileId = fid.GetString();
        }

        // A file object must provide either inline content or a storage reference.
        if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(fileId))
        {
            return null;
        }

        string? fileName = null;
        if (TryGetPropertyIgnoreCase(obj, "fileName", out var fn) && fn.ValueKind == JsonValueKind.String)
        {
            fileName = fn.GetString();
        }

        string? contentType = null;
        if (TryGetPropertyIgnoreCase(obj, "contentType", out var ct) && ct.ValueKind == JsonValueKind.String)
        {
            contentType = ct.GetString();
        }

        long? fileSize = null;
        if (TryGetPropertyIgnoreCase(obj, "fileSize", out var fs))
        {
            if (fs.ValueKind == JsonValueKind.Number && fs.TryGetInt64(out var size))
            {
                fileSize = size;
            }
            else if (fs.ValueKind == JsonValueKind.String && long.TryParse(fs.GetString(), out var sizeFromString))
            {
                fileSize = sizeFromString;
            }
        }

        return new UploadedFile(content, fileName, contentType, fileSize, fileId);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static JsonElement? ToJsonElement(object data)
    {
        if (data is JsonElement element)
        {
            return element;
        }

        if (data is string)
        {
            return null;
        }

        // Data deserialized into another shape (e.g. dictionaries from the Temporal payload
        // converter): normalize through JSON so all formats are handled uniformly.
        try
        {
            return JsonSerializer.SerializeToElement(data);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
