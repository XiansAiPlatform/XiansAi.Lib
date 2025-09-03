using System.Collections.Concurrent;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using XiansAi.Logging;
using XiansAi.Messaging;

namespace XiansAi.Flow;
public class DataHandler : SafeHandler
{
    private readonly MessageHub _messageHub;
    private readonly FlowBase _flow;
    private readonly ConcurrentQueue<MessageThread> _messageQueue = new();
    private static readonly Logger<DataHandler> _logger = Logger<DataHandler>.For();
    private bool _initialized = false;

    private ProcessDataSettings? _processDataInformation = null;
    private Type? _dataProcessorType = null;

    public DataHandler(MessageHub messageHub, FlowBase flow)
    {
        _messageHub = messageHub;
        _flow = flow;
        _messageHub.SubscribeDataHandler(EnqueueDataMessage);
    }

    public void EnqueueDataMessage(MessageThread thread) {
        if (!_initialized) {
            _logger.LogWarning("Data handler not initialized, adding message to queue for later processing");
        }
        _messageQueue.Enqueue(thread);
    }

    public async Task InitDataProcessing()
    {
        _logger.LogDebug("Initializing data handler");
        _initialized = true;
        
        // check if the data processing should be done in workflow
        _processDataInformation = await Workflow.ExecuteLocalActivityAsync(
            (SystemActivities a) => a.GetProcessDataSettings(),
            new SystemLocalActivityOptions());

        _dataProcessorType = GetProcessorType(_processDataInformation);

        while (true)
        {
            MessageThread? messageThread = null;
            try
            {
                messageThread = await DequeueMessage();
                _logger.LogDebug($"Message received from thread id: {messageThread?.ThreadId}");
                if (messageThread == null) continue;

                object? result = await ProcessData(messageThread);

                await messageThread.SendData(result);
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

    public async Task<object?> ProcessData(MessageThread messageThread)
    {
        object? result = null;
        if (_processDataInformation == null)
        {
            throw new Exception("Process data information is not set. Have you initialized the data handler?");
        }
        if (_dataProcessorType == null)
        {
            throw new Exception("Data processor type is not set. Have you initialized the data handler?");
        }
        if (_processDataInformation.ShouldProcessDataInWorkflow)
        {
            _logger.LogDebug("Processing data in workflow");
            // process the data in workflow
            result = await ProcessDataStatic(_dataProcessorType, messageThread, _flow);
        }
        else
        {
            // In activity mode, dataProcessorType should not have more than one constructor args
            if (_dataProcessorType.GetConstructors().Any(c => c.GetParameters().Length > 1))
            {
                throw new Exception("Data processor type has more than one constructor parameter, which is not supported in activity mode");
            }
            _logger.LogDebug("Processing data in activity");
            // process the data in activity
            await Workflow.ExecuteLocalActivityAsync(
                (SystemActivities a) => a.ProcessData(messageThread),
                new SystemLocalActivityOptions());
        }

        return result;
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


    public static async Task<object?> ProcessDataStatic(Type? dataProcessorType, MessageThread messageThread, FlowBase? flow)
    {
        var methodName = messageThread.LatestMessage.Content;
        var methodArgs = messageThread.LatestMessage.Data?.ToString();

        if (dataProcessorType == null)
        {
            throw new Exception("Data processor type is not set for this flow or Processing is not initialized. Use `flow.SetDataProcessor<DataProcessor>()` to set the data processor type.");
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

            return result;

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