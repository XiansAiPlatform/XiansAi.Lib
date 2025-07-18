using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.UnitTests.Flow
{
    /*
    dotnet test --filter "FullyQualifiedName~MessengerTests"
    */
    public class MessengerTests
    {

        /*
        dotnet test --filter "FullyQualifiedName~MessengerTests"
        */
        public MessengerTests()
        {
            AgentContext.AgentName = "test-agent";
            AgentContext.WorkflowId = "test-workflow-id";
            AgentContext.WorkflowType = "test-workflow-type";
        }

        [Fact]
        public async Task RegisterAsyncHandler_AddsHandlerToCollection()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCalled = false;
            
            // Act
            ConversationReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.SubscribeAsyncChatHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task UnregisterHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCalled = false;
            
            ConversationReceivedHandler handler = _ => handlerCalled = true;
            messenger.SubscribeChatHandler(handler);
            
            // Act
            messenger.UnsubscribeChatHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public async Task UnregisterAsyncHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCalled = false;
            
            ConversationReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.SubscribeAsyncChatHandler(handler);
            
            // Act
            messenger.UnsubscribeAsyncChatHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public async Task ReceiveMessage_CallsAllRegisteredHandlers()
        {
            // Arrange
            var messenger = new MessageHub();
            var syncHandlerCalled = false;
            var asyncHandlerCalled = false;
            
            ConversationReceivedHandler syncHandler = _ => syncHandlerCalled = true;
            ConversationReceivedAsyncHandler asyncHandler = _ => 
            {
                asyncHandlerCalled = true;
                return Task.CompletedTask;
            };
            
            messenger.SubscribeChatHandler(syncHandler);
            messenger.SubscribeAsyncChatHandler(asyncHandler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.True(syncHandlerCalled);
            Assert.True(asyncHandlerCalled);
        }

        [Fact]
        public async Task ReceiveMessage_CreatesProperMessageThread()
        {
            // Arrange
            var messenger = new MessageHub();
            MessageThread capturedMessageThread = null!;
            
            ConversationReceivedHandler handler = messageThread => capturedMessageThread = messageThread;
            messenger.SubscribeChatHandler(handler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.NotNull(capturedMessageThread);
            Assert.Equal(messageSignal.Payload.ParticipantId, capturedMessageThread.ParticipantId);
            Assert.Equal(AgentContext.WorkflowId, capturedMessageThread.WorkflowId);
        }

        [Fact]
        public async Task RegisteringSameHandlerMultipleTimes_OnlyRegistersOnce()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCallCount = 0;
            
            ConversationReceivedHandler handler = _ => handlerCallCount++;
            
            // Act
            messenger.SubscribeChatHandler(handler);
            messenger.SubscribeChatHandler(handler); // Try to register the same handler again
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveConversationChatOrData(messageSignal);
            
            // Assert
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public async Task UnregisteringNonExistentHandler_DoesNotThrowException()
        {
            // Arrange
            var messenger = new MessageHub();
            ConversationReceivedHandler handler = _ => { };
            
            // Act & Assert
            var exception = await Record.ExceptionAsync(() => Task.Run(() => messenger.UnsubscribeChatHandler(handler)));
            Assert.Null(exception);
        }

        private MessageSignal CreateTestMessageSignal()
        {
            return new MessageSignal
            {
                Payload = new MessagePayload
                {
                    ParticipantId = "test-participant-id",
                    Text = "Test message content",
                    RequestId = "test-request-id",
                    Hint = "test-hint",
                    Scope = "test-scope",
                    Data = new Dictionary<string, string?> { { "Type", "test" } },
                    Agent = "test-agent",
                    ThreadId = "test-thread-id",
                    Type = MessageType.Chat.ToString()
                },
                SourceWorkflowId = "test-workflow-id",
                SourceWorkflowType = "test-workflow-type",
                SourceAgent = "test-agent"
            };
        }
    }
} 