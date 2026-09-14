using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

internal sealed class ConversationStore(UserMessageContext context) : ChatMessageStore
{
    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken)
    {
        var messages = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
        return messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => new ChatMessage(
                message.Direction.Equals("outgoing", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User,
                message.Text!))
            .Reverse();
    }

    public override Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null) =>
        JsonSerializer.SerializeToElement(context.Message.ThreadId);
}
