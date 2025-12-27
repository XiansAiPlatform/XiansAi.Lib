using System.Text.Json;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Models;

namespace Xians.Agent.Sample.Utils;

/// <summary>
/// ChatMessageStore implementation that reads chat history from Xians platform.
/// Storage is handled automatically by Xians, so AddMessagesAsync is a no-op.
/// </summary>
internal sealed class XiansChatMessageStore : ChatMessageStore
{
    private readonly UserMessageContext _context;

    public XiansChatMessageStore(
        UserMessageContext context,
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Thread ID is managed by Xians through the context
        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            ThreadId = serializedStoreState.Deserialize<string>();
        }
        
        // Use Xians ThreadId if available
        ThreadId ??= context.ThreadId;
    }

    public string? ThreadId { get; private set; }

    public override async Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // No-op: Xians automatically stores messages when ReplyAsync is called
        // The messages are persisted through the Xians platform infrastructure
        await Task.CompletedTask;
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken)
    {
        // Retrieve chat history from Xians
        // Using a reasonable page size to get recent history
        // Note: GetChatHistoryAsync automatically filters out the current message
        var xiansMessages = await _context.GetChatHistoryAsync(page: 1, pageSize: 10);
        
        // Console.WriteLine($"[XiansChatMessageStore] Fetched {xiansMessages.Count} messages from server:");
        // foreach (var msg in xiansMessages)
        // {
        //     Console.WriteLine($"  - [{msg.Direction}] {msg.CreatedAt:yyyy-MM-dd HH:mm:ss} | {msg.Text}");
        // }
        
        // Convert DbMessage to ChatMessage
        var chatMessages = new List<ChatMessage>();
        foreach (var msg in xiansMessages)
        {
            var chatMessage = ConvertToChatMessage(msg);
            if (chatMessage != null)
            {
                chatMessages.Add(chatMessage);
            }
        }

        Console.WriteLine($"[XiansChatMessageStore] Converted {chatMessages.Count} messages, returning in chronological order");
        
        // Reverse to get chronological order (oldest to newest)
        // Xians returns messages in reverse chronological order (newest first)
        chatMessages.Reverse();
        return chatMessages;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        // Serialize the thread ID for state persistence
        return JsonSerializer.SerializeToElement(ThreadId ?? _context.ThreadId);
    }

    /// <summary>
    /// Converts a Xians DbMessage to a Microsoft.Extensions.AI ChatMessage.
    /// </summary>
    private ChatMessage? ConvertToChatMessage(DbMessage dbMessage)
    {
        if (string.IsNullOrEmpty(dbMessage.Text))
        {
            return null;
        }

        // Determine the role based on message direction
        var direction = dbMessage.Direction.ToLowerInvariant();
        var role = direction switch
        {
            "outgoing" or "outbound" => ChatRole.Assistant,  // Messages from the agent
            "incoming" or "inbound" => ChatRole.User,        // Messages from the user
            _ => ChatRole.User                 // Default to user
        };

        var chatMessage = new ChatMessage(role, dbMessage.Text);
        
        // Store Xians-specific metadata in AdditionalProperties
        chatMessage.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["xiansMessageId"] = dbMessage.Id,
            ["xiansThreadId"] = dbMessage.ThreadId,
            ["xiansCreatedAt"] = dbMessage.CreatedAt,
            ["xiansStatus"] = dbMessage.Status,
            ["xiansData"] = dbMessage.Data
        };

        return chatMessage;
    }
}

