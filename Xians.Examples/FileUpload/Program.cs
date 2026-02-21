using Xians.Lib.Agents.Core;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using System.Text.Json;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");
var xiansAgentCertificate = Environment.GetEnvironmentVariable("XIANS_AGENT_CERTIFICATE")
    ?? throw new InvalidOperationException("XIANS_AGENT_CERTIFICATE environment variable is not set");

var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansAgentCertificate,
    ConsoleLogLevel = LogLevel.Debug,
    ServerLogLevel = LogLevel.Warning
});

var xiansAgent = xiansPlatform.Agents.Register(new()
{
    Name = "File Upload Agent",
    IsTemplate = true
});

var conversationalWorkflow = xiansAgent.Workflows.DefineSupervisor();

conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    await context.ReplyAsync("Send a file with type=\"File\" to test file upload. I can process PDFs, images, and other documents.");
});

conversationalWorkflow.OnFileUpload(async (context) =>
{
    string base64Content;
    string? fileName = null;
    string? contentType = null;

    if (context.Message.Data is JsonElement jsonElement)
    {
        base64Content = jsonElement.TryGetProperty("content", out var contentProp)
            ? contentProp.GetString() ?? ""
            : jsonElement.GetString() ?? "";
        if (jsonElement.TryGetProperty("fileName", out var fn))
            fileName = fn.GetString();
        if (jsonElement.TryGetProperty("contentType", out var ct))
            contentType = ct.GetString();
    }
    else
    {
        base64Content = context.Message.Data?.ToString() ?? "";
        fileName = context.Message.Text;
    }

    if (string.IsNullOrEmpty(base64Content))
    {
        await context.ReplyAsync("No file data received.");
        return;
    }

    try
    {
        var fileBytes = Convert.FromBase64String(base64Content);
        var displayName = fileName ?? "uploaded-file";
        await context.ReplyAsync(
            $"File received successfully! Processed {fileBytes.Length} bytes. " +
            $"{(contentType != null ? $"Type: {contentType}. " : "")}" +
            $"Name: {displayName}");
    }
    catch (FormatException)
    {
        await context.ReplyAsync("Invalid file format. Please ensure the file is base64 encoded.");
    }
});

await xiansAgent.RunAllAsync();
