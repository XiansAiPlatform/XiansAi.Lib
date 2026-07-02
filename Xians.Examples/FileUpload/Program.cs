using Xians.Lib.Agents.Core;
using DotNetEnv;
using Microsoft.Extensions.Logging;

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
    var files = context.Message.Files;

    if (files.Count == 0)
    {
        await context.ReplyAsync("No file data received.");
        return;
    }

    var saveDirectory = Path.Combine(Path.GetTempPath(), "xians-file-uploads");
    Directory.CreateDirectory(saveDirectory);

    var summaries = new List<string>();
    foreach (var file in files)
    {
        if (!file.TryGetBytes(out var fileBytes))
        {
            await context.ReplyAsync(
                $"Invalid file format for '{file.FileName ?? "uploaded-file"}'. Please ensure the file is base64 encoded.");
            return;
        }

        var fileName = Path.GetFileName(file.FileName ?? $"uploaded-file-{Guid.NewGuid():N}");
        var savePath = Path.Combine(saveDirectory, fileName);
        await File.WriteAllBytesAsync(savePath, fileBytes!);

        summaries.Add(
            $"{fileName} ({fileBytes!.Length} bytes" +
            $"{(file.ContentType != null ? $", {file.ContentType}" : "")}) saved to {savePath}");
    }

    await context.ReplyAsync(
        $"Received {files.Count} file(s) with message '{context.Message.Text}' successfully! {string.Join(", ", summaries)}");
});

await xiansAgent.RunAllAsync();
