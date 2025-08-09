using System.Collections.Concurrent;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using XiansAi.Logging;
using XiansAi.Messaging;

namespace XiansAi.Flow;
public class DataHandler : SafeHandler
{
    private readonly IMessageHub _messageHub;
    private readonly FlowBase _flow;
    private readonly ConcurrentQueue<MessageThread> _messageQueue = new();
    private static readonly Logger<DataHandler> _logger = Logger<DataHandler>.For();

    public SystemActivityOptions SystemActivityOptions { get; set; } = new SystemActivityOptions();

    public DataHandler(IMessageHub messageHub, FlowBase flow)
    {
        _messageHub = messageHub;
        _flow = flow;
    }

    public async Task InitDataProcessing()
    {
        _logger.LogDebug("Subscribing to data handler");
        _messageHub.SubscribeDataHandler(_messageQueue.Enqueue);
        // check if the data processing should be done in workflow
        var processDataInformation = await Workflow.ExecuteLocalActivityAsync(
            (SystemActivities a) => a.GetProcessDataSettings(),
            new SystemLocalActivityOptions());

        Type? dataProcessorType = GetProcessorType(processDataInformation);

        while (true)
        {
            MessageThread? messageThread = null;
            try
            {
                messageThread = await DequeueMessage();
                _logger.LogDebug($"Message received from thread id: {messageThread?.ThreadId}");
                if (messageThread == null) continue;

                var methodName = messageThread.LatestMessage.Content;
                var parameters = messageThread.LatestMessage.Data?.ToString();

                if (processDataInformation.ShouldProcessDataInWorkflow)
                {
                    _logger.LogDebug("Processing data in workflow");
                    // process the data in workflow
                    await ProcessData(dataProcessorType, messageThread, methodName, parameters);
                }
                else
                {
                    // In activity mode, dataProcessorType should not have more than one constructor args
                    if (dataProcessorType.GetConstructors().Any(c => c.GetParameters().Length > 1))
                    {
                        throw new Exception("Data processor type has more than one constructor parameter, which is not supported in activity mode");
                    }
                    _logger.LogDebug("Processing data in activity");
                    // process the data in activity
                    await Workflow.ExecuteLocalActivityAsync(
                        (SystemActivities a) => a.ProcessData(messageThread, methodName, parameters),
                        new SystemLocalActivityOptions());
                }
            }
            catch (ContinueAsNewException)
            {
                _logger.LogDebug("DataHandler is continuing as new");
                throw;
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

    private static Type GetProcessorType(ProcessDataSettings processDataInformation)
    {
        _logger.LogDebug($"Process data information starting data processing: {processDataInformation.DataProcessorTypeName}, {processDataInformation.ShouldProcessDataInWorkflow}");

        if (processDataInformation.DataProcessorTypeName == null)
        {
            throw new Exception("Data processor type is not set for this flow. Use `flow.SetDataProcessor<DataProcessor>(bool)` to set the data processor type.");
        }
        var dataProcessorType = Type.GetType(processDataInformation.DataProcessorTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == processDataInformation.DataProcessorTypeName);

        if (dataProcessorType == null)
        {
            throw new Exception($"Data processor type {processDataInformation.DataProcessorTypeName} not found");
        }

        return dataProcessorType;
    }

    private async Task<MessageThread?> DequeueMessage()
    {
        _logger.LogDebug("Waiting for message...");
        await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0 || ShouldContinueAsNew);

        if (ShouldContinueAsNew)
        {
            // Check if we should continue as new
            ContinueAsNew();
        }
        _messageQueue.TryDequeue(out var thread);

        if (thread == null)
        {
            _logger.LogWarning("No message thread received");
        }
        return thread;
    }
    public async Task ProcessData(Type? dataProcessorType, MessageThread messageThread, string methodName, string? methodArgs) {
        await ProcessDataStatic(dataProcessorType, messageThread, _flow, methodName, methodArgs);
    }

    public static async Task ProcessDataStatic(Type? dataProcessorType, MessageThread messageThread, FlowBase? flow, string methodName, string? methodArgs)
    {
        if (dataProcessorType == null)
        {
            throw new Exception("Data processor type is not set for this flow. Use `flow.SetDataProcessor<DataProcessor>()` to set the data processor type.");
        }

        try
        {
            _logger.LogDebug($"Processing data with method {methodName} and parameters {methodArgs}");

            object[] constructorArgs = flow != null ? [messageThread, flow] : [messageThread];

            var result = await DynamicMethodInvoker.InvokeMethodAsync(
                dataProcessorType,
                constructorArgs,
                methodName,
                methodArgs);

            _logger.LogDebug($"Result returned from {methodName}: {result}");

            await messageThread.SendData(result);
        }
        catch (DynamicInvokerException ex)
        {
            _logger.LogError($"Error processing data with method {methodName}", ex);
            throw new ApplicationFailureException($"Error processing data with method {methodName}", ex, ex.GetType().FullName, true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing data with method {methodName}", ex);
            throw;
        }
    }
}