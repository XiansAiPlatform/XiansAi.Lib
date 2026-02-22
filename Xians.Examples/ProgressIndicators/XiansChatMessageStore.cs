using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

internal sealed class XiansChatMessageStore : ChatMessageStore
{
    private readonly UserMessageContext _context;

    public XiansChatMessageStore(UserMessageContext context)
    {
        _context = context;
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken)
    {
        var xiansMessages = await _context.GetChatHistoryAsync(page: 1, pageSize: 10);

        var chatMessages = xiansMessages
            .Where(msg => !string.IsNullOrEmpty(msg.Text))
            .Select(msg => new ChatMessage(
                msg.Direction.ToLowerInvariant() == "outgoing" ? ChatRole.Assistant : ChatRole.User,
                msg.Text!))
            .Reverse()
            .ToList();

        return chatMessages;
    }

    public override Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(_context.Message.ThreadId);
    }
}
