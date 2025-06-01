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
            MessageReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.RegisterAsyncMessageHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task UnregisterHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCalled = false;
            
            MessageReceivedHandler handler = _ => handlerCalled = true;
            messenger.RegisterMessageHandler(handler);
            
            // Act
            messenger.UnregisterMessageHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public async Task UnregisterAsyncHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new MessageHub();
            var handlerCalled = false;
            
            MessageReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.RegisterAsyncMessageHandler(handler);
            
            // Act
            messenger.UnregisterAsyncMessageHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
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
            
            MessageReceivedHandler syncHandler = _ => syncHandlerCalled = true;
            MessageReceivedAsyncHandler asyncHandler = _ => 
            {
                asyncHandlerCalled = true;
                return Task.CompletedTask;
            };
            
            messenger.RegisterMessageHandler(syncHandler);
            messenger.RegisterAsyncMessageHandler(asyncHandler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method
            await messenger.ReceiveMessage(messageSignal);
            
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
            
            MessageReceivedHandler handler = messageThread => capturedMessageThread = messageThread;
            messenger.RegisterMessageHandler(handler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method
            await messenger.ReceiveMessage(messageSignal);
            
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
            
            MessageReceivedHandler handler = _ => handlerCallCount++;
            
            // Act
            messenger.RegisterMessageHandler(handler);
            messenger.RegisterMessageHandler(handler); // Try to register the same handler again
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public async Task UnregisteringNonExistentHandler_DoesNotThrowException()
        {
            // Arrange
            var messenger = new MessageHub();
            MessageReceivedHandler handler = _ => { };
            
            // Act & Assert
            var exception = await Record.ExceptionAsync(() => Task.Run(() => messenger.UnregisterMessageHandler(handler)));
            Assert.Null(exception);
        }

        private MessageSignal CreateTestMessageSignal()
        {
            return new MessageSignal
            {
                Payload = new MessagePayload
                {
                    ParticipantId = "test-participant-id",
                    Content = "Test message content",
                    Metadata = new Dictionary<string, string?> { { "Type", "test" } },
                    Agent = "test-agent",
                    ThreadId = "test-thread-id"
                },
                TargetWorkflowId = "test-workflow-id",
                TargetWorkflowType = "test-workflow-type",
                SourceAgent = "test-agent"
            };
        }
    }
} 