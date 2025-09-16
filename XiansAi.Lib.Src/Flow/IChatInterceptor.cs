using Agentri.Messaging;

namespace Agentri.Flow;

public interface IChatInterceptor
{
    Task<MessageThread> InterceptIncomingMessageAsync(MessageThread messageThread);
    Task<string?> InterceptOutgoingMessageAsync(MessageThread messageThread, string? response);
}