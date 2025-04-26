using XiansAi.Flow;
using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.UnitTests.Flow
{
    /*
    dotnet test --filter "FullyQualifiedName~MessengerTests"
    */
    public class MessengerTests
    {
        private readonly string _workflowId = "test-workflow-id";
        private readonly string _workflowType = "test-workflow-type";

        [Fact]
        public async Task RegisterHandler_AddsHandlerToCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var handlerCalled = false;
            
            // Act
            MessageReceivedHandler handler = _ => handlerCalled = true;
            messenger.RegisterHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task RegisterAsyncHandler_AddsHandlerToCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var handlerCalled = false;
            
            // Act
            MessageReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.RegisterAsyncHandler(handler);
            
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
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var handlerCalled = false;
            
            MessageReceivedHandler handler = _ => handlerCalled = true;
            messenger.RegisterHandler(handler);
            
            // Act
            messenger.UnregisterHandler(handler);
            
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
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var handlerCalled = false;
            
            MessageReceivedAsyncHandler handler = _ => 
            {
                handlerCalled = true;
                return Task.CompletedTask;
            };
            messenger.RegisterAsyncHandler(handler);
            
            // Act
            messenger.UnregisterAsyncHandler(handler);
            
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
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var syncHandlerCalled = false;
            var asyncHandlerCalled = false;
            
            MessageReceivedHandler syncHandler = _ => syncHandlerCalled = true;
            MessageReceivedAsyncHandler asyncHandler = _ => 
            {
                asyncHandlerCalled = true;
                return Task.CompletedTask;
            };
            
            messenger.RegisterHandler(syncHandler);
            messenger.RegisterAsyncHandler(asyncHandler);
            
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
            var messenger = new Messenger(_workflowId, _workflowType, null);
            MessageThread capturedMessageThread = null!;
            
            MessageReceivedHandler handler = messageThread => capturedMessageThread = messageThread;
            messenger.RegisterHandler(handler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.NotNull(capturedMessageThread);
            Assert.Equal(messageSignal.ParticipantId, capturedMessageThread.ParticipantId);
            Assert.Equal(_workflowId, capturedMessageThread.WorkflowId);

        }

        [Fact]
        public async Task RegisteringSameHandlerMultipleTimes_OnlyRegistersOnce()
        {
            // Arrange
            var messenger = new Messenger(_workflowId, _workflowType, null);
            var handlerCallCount = 0;
            
            MessageReceivedHandler handler = _ => handlerCallCount++;
            
            // Act
            messenger.RegisterHandler(handler);
            messenger.RegisterHandler(handler); // Try to register the same handler again
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the now public method to trigger the handler
            await messenger.ReceiveMessage(messageSignal);
            
            // Assert
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public void UnregisteringNonExistentHandler_DoesNotThrowException()
        {
            // Arrange
            var messenger = new Messenger(_workflowId, _workflowType, null);
            MessageReceivedHandler handler = _ => { };
            
            // Act & Assert
            var exception = Record.Exception(() => messenger.UnregisterHandler(handler));
            Assert.Null(exception);
        }

        private MessageSignal CreateTestMessageSignal()
        {
            return new MessageSignal
            {
                ParticipantId = "test-participant-id",
                Content = "Test message content",
                Metadata = new { Type = "test" },
            };
        }
    }
} 