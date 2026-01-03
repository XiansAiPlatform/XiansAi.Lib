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
        // Get chat history from Xians
        var xiansMessages = await _context.Message.GetHistoryAsync(page: 1, pageSize: 10);
        
        // Convert to ChatMessage format
        var chatMessages = xiansMessages
            .Where(msg => !string.IsNullOrEmpty(msg.Text))
            .Select(msg => new ChatMessage(
                msg.Direction.ToLowerInvariant() == "outgoing" ? ChatRole.Assistant : ChatRole.User,
                msg.Text!))
            .Reverse() // Xians returns newest first, we need oldest first
            .ToList();
        
        return chatMessages;
    }

    public override Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // No-op: Xians automatically stores messages
        return Task.CompletedTask;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        // Serialize the thread ID for state persistence
        return JsonSerializer.SerializeToElement(_context.ThreadId);
    }
}

