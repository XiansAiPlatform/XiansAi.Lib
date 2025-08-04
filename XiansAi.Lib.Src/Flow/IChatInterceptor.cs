using XiansAi.Messaging;

namespace XiansAi.Flow;

public interface IChatInterceptor
{
    Task<MessageThread> InterceptIncomingMessageAsync(MessageThread messageThread);
    Task<string?> InterceptOutgoingMessageAsync(MessageThread messageThread, string? response);
}