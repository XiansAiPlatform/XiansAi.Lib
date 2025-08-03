using System.Collections.Concurrent;
using Temporalio.Workflows;
using XiansAi.Logging;
using XiansAi.Messaging;

namespace XiansAi.Flow;
public class DataHandler
{
    private readonly IMessageHub _messageHub;
    private readonly ConcurrentQueue<MessageThread> _messageQueue = new();
    private static readonly Logger<DataHandler> _logger = Logger<DataHandler>.For();

    public SystemActivityOptions SystemActivityOptions { get; set; } = new SystemActivityOptions();

    public DataHandler(IMessageHub messageHub)
    {
        _messageHub = messageHub;
        _messageHub.SubscribeDataHandler(_messageQueue.Enqueue);
    }

    public async Task InitDataProcessing()
    {
        while (true)
        {
            MessageThread? messageThread = null;
            try
            {
                messageThread = await DequeueMessage();
                if (messageThread == null) continue;

                var methodName = messageThread.LatestMessage.Content;
                var parameters = messageThread.LatestMessage.Data?.ToString();
                _logger.LogDebug("Method name: " + methodName);
                _logger.LogDebug("Parameters: " + parameters?.ToString());

                var result = await Workflow.ExecuteActivityAsync(
                    (SystemActivities a) => a.ProcessData(messageThread, methodName, parameters),
                    SystemActivityOptions);
                
                await messageThread.SendData(result);

            }
            catch (Exception ex)
            {
                _logger.LogError("Error in DataHandler", ex);
                
                // Send error message back to the caller
                if (messageThread != null)
                {
                    try
                    {
                        await messageThread.SendData(new { error = ex.Message }, "Error occurred while processing data");
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogError("Failed to send error message", sendEx);
                    }
                }
            }
        }
    }

    private async Task<MessageThread?> DequeueMessage()
    {
        _logger.LogDebug("Waiting for message...");
        await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);
        _logger.LogDebug("Message received");
        return _messageQueue.TryDequeue(out var thread) ? thread : null;
    }

    public static async Task<object?> ProcessData(Type? dataProcessorType, MessageThread messageThread, string methodName, string? parameters)
    {
        if (dataProcessorType == null)
        {
            throw new Exception("Data processor type is not set for this flow. Use `flow.SetDataProcessor<DataProcessor>()` to set the data processor type.");
        }

        try
        {
            _logger.LogDebug($"Processing data with method {methodName} and parameters {parameters}");
            
            var result = await DynamicMethodInvoker.InvokeMethodAsync(
                dataProcessorType, 
                messageThread, 
                methodName, 
                parameters);

            _logger.LogDebug($"Result returned from {methodName}: {result}");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing data with method {methodName}", ex);
            throw;
        }
    }
}