using OpenAI.Chat;
using Microsoft.Extensions.Logging;
public interface IOpenAIClientService
{
    Task<string> GetChatCompletionAsync(List<ChatMessage> messages);
}

public class OpenAIClientService : IOpenAIClientService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAIClientService>? _logger;
    public OpenAIClientService(string model, string apiKey, ILogger<OpenAIClientService>? logger = null)
    {
        _logger = logger;
        LogInformation("OpenAIClientService constructor called with model: {0}", model);
        LogInformation("OpenAIClientService constructor called with apiKey: {0}...{1}", apiKey.Substring(0, 2), apiKey.Substring(apiKey.Length - 2));
        _chatClient = new ChatClient(model, apiKey);
    }

    public async Task<string> GetChatCompletionAsync(List<ChatMessage> messages)
    {
        var completion = await _chatClient.CompleteChatAsync(messages);
        var text = completion.Value.Content[0].Text;
        return text;
    }

    private void LogInformation(string message, params object[] args)
    {
        if (_logger != null)
        {
            _logger.LogInformation(message, args);
        } 
        else
        {
            Console.WriteLine(message, args);
        }
    }

}
