using XiansAi.Flow;

namespace XiansAi.Lib.Tests.UnitTests.Flow
{
    /*
    dotnet test --filter "FullyQualifiedName~MessengerTests"
    */
    public class MessengerTests
    {
        private readonly string _workflowId = "test-workflow-id";

        [Fact]
        public void RegisterHandler_AddsHandlerToCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
            var handlerCalled = false;
            
            // Act
            MessageReceivedHandler handler = _ => handlerCalled = true;
            messenger.RegisterHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the internal method to trigger the handler
            messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(messenger, new object[] { messageSignal });
            
            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task RegisterAsyncHandler_AddsHandlerToCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
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
            
            // Use the internal method to trigger the handler
            var method = messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                
            var task = (Task)method.Invoke(messenger, new object[] { messageSignal })!;
            await task;
            
            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public void UnregisterHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
            var handlerCalled = false;
            
            MessageReceivedHandler handler = _ => handlerCalled = true;
            messenger.RegisterHandler(handler);
            
            // Act
            messenger.UnregisterHandler(handler);
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the internal method to trigger the handler
            messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(messenger, new object[] { messageSignal });
            
            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public void UnregisterAsyncHandler_RemovesHandlerFromCollection()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
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
            
            // Use the internal method to trigger the handler
            messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(messenger, new object[] { messageSignal });
            
            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public async Task ReceiveMessage_CallsAllRegisteredHandlers()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
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
            
            // Use reflection to access the internal method
            var method = messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                
            var task = (Task)method.Invoke(messenger, new object[] { messageSignal })!;
            await task;
            
            // Assert
            Assert.True(syncHandlerCalled);
            Assert.True(asyncHandlerCalled);
        }

        [Fact]
        public async Task ReceiveMessage_CreatesProperMessageThread()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
            MessageThread capturedMessageThread = null!;
            
            MessageReceivedHandler handler = messageThread => capturedMessageThread = messageThread;
            messenger.RegisterHandler(handler);
            
            // Act
            var messageSignal = CreateTestMessageSignal();
            
            // Use reflection to access the internal method
            var method = messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                
            await (Task)method.Invoke(messenger, new object[] { messageSignal })!;
            
            // Assert
            Assert.NotNull(capturedMessageThread);
            Assert.Equal(messageSignal.ThreadId, capturedMessageThread.ThreadId);
            Assert.Equal(messageSignal.ParticipantId, capturedMessageThread.ParticipantId);
            Assert.Equal(_workflowId, capturedMessageThread.WorkflowId);
            Assert.NotNull(capturedMessageThread.IncomingMessage);
            Assert.Equal(messageSignal.Content, capturedMessageThread.IncomingMessage.Content);
            Assert.Equal(messageSignal.Metadata, capturedMessageThread.IncomingMessage.Metadata);
            Assert.Equal(messageSignal.CreatedAt, capturedMessageThread.IncomingMessage.CreatedAt);
            Assert.Equal(messageSignal.CreatedBy, capturedMessageThread.IncomingMessage.CreatedBy);
        }

        [Fact]
        public void RegisteringSameHandlerMultipleTimes_OnlyRegistersOnce()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
            var handlerCallCount = 0;
            
            MessageReceivedHandler handler = _ => handlerCallCount++;
            
            // Act
            messenger.RegisterHandler(handler);
            messenger.RegisterHandler(handler); // Try to register the same handler again
            
            // Create a message signal to test with
            var messageSignal = CreateTestMessageSignal();
            
            // Use the internal method to trigger the handler
            messenger.GetType()
                .GetMethod("ReceiveMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(messenger, new object[] { messageSignal });
            
            // Assert
            Assert.Equal(1, handlerCallCount);
        }

        [Fact]
        public void UnregisteringNonExistentHandler_DoesNotThrowException()
        {
            // Arrange
            var messenger = new Messenger(_workflowId);
            MessageReceivedHandler handler = _ => { };
            
            // Act & Assert
            var exception = Record.Exception(() => messenger.UnregisterHandler(handler));
            Assert.Null(exception);
        }

        private MessageSignal CreateTestMessageSignal()
        {
            return new MessageSignal
            {
                ThreadId = "test-thread-id",
                ParticipantId = "test-participant-id",
                Content = "Test message content",
                Metadata = new { Type = "test" },
                CreatedAt = DateTime.UtcNow.ToString("o"),
                CreatedBy = "test-user"
            };
        }
    }
} 