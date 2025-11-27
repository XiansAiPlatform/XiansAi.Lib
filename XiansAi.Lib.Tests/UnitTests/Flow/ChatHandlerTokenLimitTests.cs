using System.Threading.Tasks;
using XiansAi.Exceptions;
using XiansAi.Flow;
using XiansAi.Flow.Router;
using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.UnitTests.Flow;

public class ChatHandlerTokenLimitTests
{
    [Fact]
    public async Task ProcessMessage_WhenTokenLimitExceeded_NotifiesAndRethrows()
    {
        var routeFunc = new Func<MessageThread, string, RouterOptions, Task<string?>>(
            (_, _, _) => throw new TokenLimitExceededException("limit hit"));

        var handler = new ChatHandler(new MessageHub(), routeFunc)
        {
            SystemPrompt = "You are helpful."
        };

        var thread = new MessageThread
        {
            ParticipantId = "user-1",
            WorkflowId = "tenant:flow",
            WorkflowType = "tenant:flow",
            Agent = "agent-1",
            ThreadId = "thread-1",
            LatestMessage = new Message
            {
                Content = "hello",
                Data = null,
                Type = MessageType.Chat,
                RequestId = "req-1",
                Hint = null,
                Scope = "scope-1",
                Origin = "user"
            }
        };

        var notified = false;
        handler.TokenLimitExceeded += (t, details) =>
        {
            notified = true;
            Assert.Equal(thread, t);
            Assert.Equal("limit hit", details);
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<TokenLimitExceededException>(() => handler.ProcessMessage(thread));

        Assert.Equal("limit hit", exception.Message);
        Assert.True(notified);
    }
}

