using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

namespace Xians.Examples.HITLAgent;

/// <summary>
/// Loads recent Xians chat history so the model can continue the conversation.
/// New messages are stored by the platform when the handler replies, so this store does not write them.
/// </summary>
internal sealed class XiansChatMessageStore : ChatMessageStore
{
    private readonly UserMessageContext _context;

    public XiansChatMessageStore(UserMessageContext context)
    {
        _context = context;
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken)
    {
        var history = await _context.GetChatHistoryAsync(page: 1, pageSize: 10);
        return history
            .Where(message => !string.IsNullOrEmpty(message.Text))
            .Select(message => new ChatMessage(
                message.Direction.Equals("outgoing", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User,
                message.Text!))
            .Reverse()
            .ToList();
    }

    public override Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(_context.Message.ThreadId);
    }
}
