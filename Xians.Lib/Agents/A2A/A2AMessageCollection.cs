using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Specialized message for Agent-to-Agent communication.
/// Simple data holder - response capturing is handled by A2AMessageContext.
/// </summary>
public class A2ACurrentMessage : CurrentMessage
{
    internal A2ACurrentMessage(
        string text, 
        string participantId,
        string requestId,
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string? authorization,
        string? threadId)
        : base(text, participantId, requestId, scope, hint, data, tenantId, authorization, threadId)
    {
    }
}


