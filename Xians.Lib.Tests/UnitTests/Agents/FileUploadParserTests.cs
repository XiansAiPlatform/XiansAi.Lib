using System.Text.Json;
using Xunit;
using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Tests.UnitTests.Agents;

public class FileUploadParserTests
{
    private static readonly string SampleBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });

    private static JsonElement ToJsonElement(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void Parse_MultiFileFormat_ReturnsAllFilesWithMetadata()
    {
        var json = $$"""
        {
            "files": [
                { "content": "{{SampleBase64}}", "fileName": "report.pdf", "contentType": "application/pdf", "fileSize": 1024 },
                { "content": "{{SampleBase64}}", "fileName": "photo.png", "contentType": "image/png" }
            ]
        }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal(2, files.Count);

        Assert.Equal(SampleBase64, files[0].Content);
        Assert.Equal("report.pdf", files[0].FileName);
        Assert.Equal("application/pdf", files[0].ContentType);
        Assert.Equal(1024, files[0].FileSize);

        Assert.Equal("photo.png", files[1].FileName);
        Assert.Equal("image/png", files[1].ContentType);
        Assert.Null(files[1].FileSize);
    }

    [Fact]
    public void Parse_SingleFileObjectFormat_ReturnsOneFile()
    {
        var json = $$"""
        { "content": "{{SampleBase64}}", "fileName": "invoice.pdf", "contentType": "application/pdf" }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        var file = Assert.Single(files);
        Assert.Equal(SampleBase64, file.Content);
        Assert.Equal("invoice.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public void Parse_RawBase64JsonString_UsesFallbackFileName()
    {
        var element = ToJsonElement($"\"{SampleBase64}\"");

        var files = FileUploadParser.Parse(element, fallbackFileName: "from-text.pdf");

        var file = Assert.Single(files);
        Assert.Equal(SampleBase64, file.Content);
        Assert.Equal("from-text.pdf", file.FileName);
    }

    [Fact]
    public void Parse_PlainString_ReturnsOneFile()
    {
        var files = FileUploadParser.Parse(SampleBase64, fallbackFileName: "caption.txt");

        var file = Assert.Single(files);
        Assert.Equal(SampleBase64, file.Content);
        Assert.Equal("caption.txt", file.FileName);
    }

    [Fact]
    public void Parse_TopLevelArray_ReturnsAllFiles()
    {
        var json = $$"""
        [
            { "content": "{{SampleBase64}}", "fileName": "a.txt" },
            { "content": "{{SampleBase64}}", "fileName": "b.txt" }
        ]
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal(2, files.Count);
        Assert.Equal("a.txt", files[0].FileName);
        Assert.Equal("b.txt", files[1].FileName);
    }

    [Fact]
    public void Parse_CaseInsensitivePropertyNames_ReturnsMetadata()
    {
        var json = $$"""
        { "files": [{ "Content": "{{SampleBase64}}", "filename": "doc.pdf", "CONTENTTYPE": "application/pdf", "FileSize": 42 }] }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        var file = Assert.Single(files);
        Assert.Equal(SampleBase64, file.Content);
        Assert.Equal("doc.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(42, file.FileSize);
    }

    [Fact]
    public void Parse_FileSizeAsString_IsParsed()
    {
        var json = $$"""
        { "content": "{{SampleBase64}}", "fileSize": "2048" }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal(2048, Assert.Single(files).FileSize);
    }

    [Fact]
    public void Parse_NullData_ReturnsEmpty()
    {
        Assert.Empty(FileUploadParser.Parse(null));
    }

    [Fact]
    public void Parse_ObjectWithoutContent_ReturnsEmpty()
    {
        var files = FileUploadParser.Parse(ToJsonElement("""{ "fileName": "no-content.pdf" }"""));

        Assert.Empty(files);
    }

    [Fact]
    public void Parse_EntriesWithoutContent_AreSkipped()
    {
        var json = $$"""
        { "files": [{ "fileName": "missing.pdf" }, { "content": "{{SampleBase64}}", "fileName": "ok.pdf" }] }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal("ok.pdf", Assert.Single(files).FileName);
    }

    [Fact]
    public void Parse_AnonymousObject_IsNormalizedThroughJson()
    {
        var data = new
        {
            files = new[]
            {
                new { content = SampleBase64, fileName = "typed.pdf", contentType = "application/pdf", fileSize = 99 }
            }
        };

        var files = FileUploadParser.Parse(data);

        var file = Assert.Single(files);
        Assert.Equal("typed.pdf", file.FileName);
        Assert.Equal(99, file.FileSize);
    }

    [Fact]
    public void UploadedFile_GetBytes_DecodesBase64()
    {
        var file = new UploadedFile(SampleBase64);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, file.GetBytes());
    }

    [Fact]
    public void UploadedFile_TryGetBytes_InvalidBase64_ReturnsFalse()
    {
        var file = new UploadedFile("not-valid-base64!!!");

        Assert.False(file.TryGetBytes(out var bytes));
        Assert.Null(bytes);
    }

    [Fact]
    public void CurrentMessage_Files_DecodesMultiFilePayload()
    {
        var json = $$"""
        { "files": [{ "content": "{{SampleBase64}}", "fileName": "report.pdf" }] }
        """;

        var message = new CurrentMessage(
            text: "here are the files",
            participantId: "user@example.com",
            requestId: "req-1",
            scope: null,
            hint: null,
            data: ToJsonElement(json),
            tenantId: "default");

        var file = Assert.Single(message.Files);
        Assert.Equal("report.pdf", file.FileName);
        Assert.Same(message.Files, message.Files); // cached
    }

    [Fact]
    public void Parse_ReferenceFile_WithFileIdAndNoContent_ReturnsReference()
    {
        var json = """
        { "files": [{ "fileId": "abc123", "fileName": "report.pdf", "contentType": "application/pdf", "fileSize": 1024 }] }
        """;

        var file = Assert.Single(FileUploadParser.Parse(ToJsonElement(json)));

        Assert.Equal("abc123", file.FileId);
        Assert.Equal("report.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(1024, file.FileSize);
        Assert.True(file.IsReference);
        Assert.Equal(string.Empty, file.Content);
    }

    [Fact]
    public void Parse_ReferenceFiles_MultipleReferences_ReturnsAll()
    {
        var json = """
        { "files": [
            { "fileId": "id-1", "fileName": "a.pdf" },
            { "fileId": "id-2", "fileName": "b.pdf" }
        ] }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal(2, files.Count);
        Assert.Equal("id-1", files[0].FileId);
        Assert.Equal("id-2", files[1].FileId);
        Assert.All(files, f => Assert.True(f.IsReference));
    }

    [Fact]
    public void Parse_MixedInlineAndReferenceFiles_ReturnsBoth()
    {
        var json = $$"""
        { "files": [
            { "content": "{{SampleBase64}}", "fileName": "inline.pdf" },
            { "fileId": "ref-1", "fileName": "reference.pdf" }
        ] }
        """;

        var files = FileUploadParser.Parse(ToJsonElement(json));

        Assert.Equal(2, files.Count);
        Assert.False(files[0].IsReference);
        Assert.Equal(SampleBase64, files[0].Content);
        Assert.True(files[1].IsReference);
        Assert.Equal("ref-1", files[1].FileId);
    }

    [Fact]
    public void Parse_ObjectWithoutContentOrFileId_ReturnsEmpty()
    {
        var files = FileUploadParser.Parse(ToJsonElement("""{ "fileName": "no-ref.pdf" }"""));

        Assert.Empty(files);
    }

    [Fact]
    public void UploadedFile_ReferenceFile_ResolvingContent_ClearsIsReference()
    {
        var file = new UploadedFile(content: null, fileName: "r.pdf", contentType: null, fileSize: null, fileId: "id-9");

        Assert.True(file.IsReference);
        Assert.Equal(string.Empty, file.Content);

        file.Content = SampleBase64;

        Assert.False(file.IsReference);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, file.GetBytes());
    }

    [Fact]
    public void UploadedFile_WithNeitherContentNorFileId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new UploadedFile(content: null, fileName: "x", contentType: null, fileSize: null, fileId: null));
    }

    [Fact]
    public void CurrentMessage_Files_RawBase64String_UsesTextAsFileName()
    {
        var message = new CurrentMessage(
            text: "upload.bin",
            participantId: "user@example.com",
            requestId: "req-2",
            scope: null,
            hint: null,
            data: SampleBase64,
            tenantId: "default");

        var file = Assert.Single(message.Files);
        Assert.Equal("upload.bin", file.FileName);
        Assert.Equal(SampleBase64, file.Content);
    }
}
