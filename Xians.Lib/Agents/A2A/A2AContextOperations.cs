using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Provides simplified A2A (Agent-to-Agent) operations through XiansContext.
/// 
/// CONTEXT REQUIREMENTS:
/// A2A works in Temporal contexts (workflows and activities):
/// - From workflow: Handler executes in isolated activity
/// - From activity: Handler executes directly (no nested activities)
/// 
/// USAGE:
/// Within workflows/activities:
///   var response = await XiansContext.A2A.SendChatToBuiltInAsync("WebWorkflow", message);
/// 
/// EXAMPLE:
/// See Xians.Examples/LeadDiscoveryAgent/ContentDiscovery/ContentDiscoveryWorkflow.cs
/// for a real-world production example of A2A communication.
/// </summary>
public class A2AContextOperations
{
    private readonly ILogger<A2AContextOperations> _logger;

    internal A2AContextOperations()
    {
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<A2AContextOperations>();
    }

    /// <summary>
    /// Sends an A2A chat message to a target workflow instance.
    /// Chat messages are routed to OnUserChatMessage or OnUserMessage handlers.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow instance.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>The response message from the target workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when targetWorkflow or message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request fails.</exception>
    public async Task<A2AMessage> SendChatAsync(XiansWorkflow targetWorkflow, A2AMessage message)
    {
        var client = new A2AClient(targetWorkflow);
        return await client.SendMessageAsync(message);
    }

    /// <summary>
    /// Sends an A2A chat message to a built-in workflow by name.
    /// Chat messages are routed to OnUserChatMessage or OnUserMessage handlers.
    /// Uses XiansContext.CurrentAgent to determine the agent (requires workflow/activity context).
    /// </summary>
    /// <param name="builtInWorkflowName">The built-in workflow name (e.g., "WebWorkflow", "Conversational").</param>
    /// <param name="message">The message to send.</param>
    /// <returns>The response message from the target workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when builtInWorkflowName or message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request fails or not in workflow/activity context.</exception>
    public async Task<A2AMessage> SendChatToBuiltInAsync(string builtInWorkflowName, A2AMessage message)
    {
        ValidateNotNullOrWhiteSpace(builtInWorkflowName, nameof(builtInWorkflowName));

        var targetWorkflow = XiansContext.GetBuiltInWorkflow(builtInWorkflowName);
        return await SendChatAsync(targetWorkflow, message);
    }


    /// <summary>
    /// Sends a chat message to the Supervisor workflow.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="participantId">The participant ID to send the message to.</param>
    /// <param name="scope">The scope of the message.</param>
    /// <param name="hint">The hint for the message.</param>
    /// <returns>The response message from the Supervisor workflow.</returns>
    public async Task<A2AMessage> SendChatToSupervisorAsync(string message, string? participantId = null, string? scope = null, string? hint = null)
    {
        var targetWorkflow = XiansContext.GetBuiltInWorkflow(WorkflowConstants.WorkflowTypes.Supervisor);
        return await SendChatAsync(targetWorkflow, new A2AMessage { Text = message, ParticipantId = participantId, Scope = scope, Hint = hint });
    }

    /// <summary>
    /// Sends a chat message to the Supervisor workflow with message history fetched from the server.
    /// The history is included in the message Data property for the supervisor to utilize.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="participantId">The participant ID to send the message to. If not provided, uses the current participant ID from context.</param>
    /// <param name="scope">The scope of the message.</param>
    /// <param name="hint">The hint for the message.</param>
    /// <param name="historyPageSize">The number of historical messages to fetch (default: 10).</param>
    /// <param name="historyPage">The page number for message history (default: 1).</param>
    /// <returns>The response message from the Supervisor workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or history fetch fails.</exception>
    public async Task<A2AMessage> SendChatToSupervisorWithHistoryAsync(
        string message, 
        string? participantId = null, 
        string? scope = null, 
        string? hint = null,
        int historyPageSize = 10,
        int historyPage = 1)
    {
        // Use provided participantId or fall back to current context
        var effectiveParticipantId = participantId ?? XiansContext.GetParticipantId();
        var effectiveScope = scope ?? string.Empty;

        // Fetch message history from the server
        var historyRequest = new Xians.Lib.Temporal.Workflows.Messaging.Models.GetMessageHistoryRequest
        {
            WorkflowId = XiansContext.WorkflowId,
            WorkflowType = XiansContext.WorkflowType,
            ParticipantId = effectiveParticipantId,
            Scope = effectiveScope,
            TenantId = XiansContext.TenantId,
            Page = historyPage,
            PageSize = historyPageSize
        };

        // Get current agent and create message activity executor
        var agent = XiansContext.CurrentAgent;
        var executorLogger = Common.Infrastructure.LoggerFactory.CreateLogger<Messaging.MessageActivityExecutor>();
        var historyExecutor = new Messaging.MessageActivityExecutor(agent, executorLogger);
        
        // Execute the history fetch via activity executor to respect workflow/activity context
        var history = await historyExecutor.GetHistoryAsync(historyRequest);

        _logger.LogInformation(
            "Fetched {HistoryCount} messages for supervisor chat with history",
            history.Count);

        // Create message data with history included
        var messageData = new Dictionary<string, object>
        {
            { "messageHistory", history },
            { "historyPageSize", historyPageSize },
            { "historyPage", historyPage }
        };

        var targetWorkflow = XiansContext.GetBuiltInWorkflow(WorkflowConstants.WorkflowTypes.Supervisor);
        return await SendChatAsync(targetWorkflow, new A2AMessage 
        { 
            Text = message, 
            ParticipantId = effectiveParticipantId, 
            Scope = effectiveScope, 
            Hint = hint,
            Data = messageData
        });
    }

    /// <summary>
    /// Sends a simple chat text message to a built-in workflow.
    /// Convenience method for sending text-only chat messages.
    /// </summary>
    /// <param name="builtInWorkflowName">The built-in workflow name.</param>
    /// <param name="messageText">The text message to send.</param>
    /// <returns>The response message from the target workflow.</returns>
    public async Task<A2AMessage> SendTextAsync(string builtInWorkflowName, string messageText)
    {
        return await SendChatToBuiltInAsync(builtInWorkflowName, new A2AMessage { Text = messageText });
    }

    #region Data Message Methods

    /// <summary>
    /// Sends an A2A data message to a target workflow instance.
    /// Data messages are routed to OnUserDataMessage handlers.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow instance.</param>
    /// <param name="message">The message to send (Data property is the primary payload).</param>
    /// <returns>The response message from the target workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when targetWorkflow or message is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request fails.</exception>
    public async Task<A2AMessage> SendDataAsync(XiansWorkflow targetWorkflow, A2AMessage message)
    {
        if (targetWorkflow == null)
        {
            throw new ArgumentNullException(nameof(targetWorkflow));
        }

        var client = new A2AClient(targetWorkflow);
        return await client.SendDataMessageAsync(message);
    }

    /// <summary>
    /// Sends an A2A data message to a built-in workflow by name.
    /// Data messages are routed to OnUserDataMessage handlers.
    /// </summary>
    /// <param name="builtInWorkflowName">The built-in workflow name.</param>
    /// <param name="message">The message to send (Data property is the primary payload).</param>
    /// <returns>The response message from the target workflow.</returns>
    public async Task<A2AMessage> SendDataToBuiltInAsync(string builtInWorkflowName, A2AMessage message)
    {
        ValidateNotNullOrWhiteSpace(builtInWorkflowName, nameof(builtInWorkflowName));

        var targetWorkflow = XiansContext.GetBuiltInWorkflow(builtInWorkflowName);
        return await SendDataAsync(targetWorkflow, message);
    }

    #endregion

    /// <summary>
    /// Attempts to send an A2A chat message to a target workflow instance.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    /// <param name="targetWorkflow">The target workflow instance.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: true if message was sent successfully
    /// - Response: the response message (null if failed)
    /// - ErrorMessage: error description (null if successful)
    /// </returns>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendChatAsync(
        XiansWorkflow targetWorkflow,
        A2AMessage message)
    {
        return TrySendAsync(
            () => SendChatAsync(targetWorkflow, message),
            "A2A chat message send failed: Target={Target}",
            targetWorkflow?.WorkflowType);
    }

    /// <summary>
    /// Attempts to send an A2A chat message to a built-in workflow.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    /// <param name="builtInWorkflowName">The built-in workflow name.</param>
    /// <param name="message">The message to send.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: true if message was sent successfully
    /// - Response: the response message (null if failed)
    /// - ErrorMessage: error description (null if successful)
    /// </returns>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendChatToBuiltInAsync(
        string builtInWorkflowName, 
        A2AMessage message)
    {
        return TrySendAsync(
            () => SendChatToBuiltInAsync(builtInWorkflowName, message),
            "A2A chat message send failed: Target={Target}",
            builtInWorkflowName);
    }

    #region Try Data Message Methods

    /// <summary>
    /// Attempts to send an A2A data message to a target workflow instance.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendDataAsync(
        XiansWorkflow targetWorkflow,
        A2AMessage message)
    {
        return TrySendAsync(
            () => SendDataAsync(targetWorkflow, message),
            "A2A data message send failed: Target={Target}",
            targetWorkflow?.WorkflowType);
    }

    /// <summary>
    /// Attempts to send an A2A data message to a built-in workflow.
    /// Returns success/failure without throwing exceptions.
    /// </summary>
    public Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendDataToBuiltInAsync(
        string builtInWorkflowName, 
        A2AMessage message)
    {
        return TrySendAsync(
            () => SendDataToBuiltInAsync(builtInWorkflowName, message),
            "A2A data message send failed: Target={Target}",
            builtInWorkflowName);
    }

    #endregion

    /// <summary>
    /// Helper method to reduce duplication in Try* methods.
    /// </summary>
    private async Task<(bool Success, A2AMessage? Response, string? ErrorMessage)> TrySendAsync(
        Func<Task<A2AMessage>> sendOperation,
        string errorMessageTemplate,
        object? target)
    {
        try
        {
            var response = await sendOperation();
            return (true, response, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, errorMessageTemplate, target);
            return (false, null, ex.Message);
        }
    }

    #region Custom Workflow Signal/Query/Update Methods

    /// <summary>
    /// Sends a signal to a custom workflow by workflow ID.
    /// The target workflow must have a [WorkflowSignal] handler with the specified name.
    /// Automatically uses activities when in workflow context, direct execution when in activity context.
    /// </summary>
    /// <param name="workflowId">The target workflow ID.</param>
    /// <param name="signalName">The name of the signal to send.</param>
    /// <param name="args">Arguments to pass to the signal handler.</param>
    /// <exception cref="ArgumentNullException">Thrown when workflowId or signalName is null.</exception>
    public async Task SendSignalAsync(
        string workflowId, 
        string signalName, 
        params object[] args)
    {
        ValidateNotNullOrWhiteSpace(workflowId, nameof(workflowId));
        ValidateNotNullOrWhiteSpace(signalName, nameof(signalName));

        var executor = new A2ASignalQueryExecutor(_logger);
        await executor.ExecuteSignalAsync(workflowId, signalName, args);
    }


    /// <summary>
    /// Queries a custom workflow by workflow ID and returns the result.
    /// The target workflow must have a [WorkflowQuery] handler with the specified name.
    /// Automatically uses activities when in workflow context, direct execution when in activity context.
    /// </summary>
    /// <typeparam name="TResult">The return type of the query.</typeparam>
    /// <param name="workflowId">The target workflow ID.</param>
    /// <param name="queryName">The name of the query.</param>
    /// <param name="args">Arguments to pass to the query handler.</param>
    /// <returns>The result of the query.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowId or queryName is null.</exception>
    public async Task<TResult> QueryAsync<TResult>(
        string workflowId, 
        string queryName, 
        params object[] args)
    {
        ValidateNotNullOrWhiteSpace(workflowId, nameof(workflowId));
        ValidateNotNullOrWhiteSpace(queryName, nameof(queryName));

        var executor = new A2ASignalQueryExecutor(_logger);
        return await executor.ExecuteQueryAsync<TResult>(workflowId, queryName, args);
    }

    /// <summary>
    /// Sends an update to a custom workflow by workflow ID and waits for the result.
    /// The target workflow must have a [WorkflowUpdate] handler with the specified name.
    /// Requires Temporal Server 1.20+.
    /// Automatically uses activities when in workflow context, direct execution when in activity context.
    /// </summary>
    /// <typeparam name="TResult">The return type of the update.</typeparam>
    /// <param name="workflowId">The target workflow ID.</param>
    /// <param name="updateName">The name of the update.</param>
    /// <param name="args">Arguments to pass to the update handler.</param>
    /// <returns>The result of the update.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowId or updateName is null.</exception>
    public async Task<TResult> UpdateAsync<TResult>(
        string workflowId, 
        string updateName, 
        params object[] args)
    {
        ValidateNotNullOrWhiteSpace(workflowId, nameof(workflowId));
        ValidateNotNullOrWhiteSpace(updateName, nameof(updateName));

        var executor = new A2ASignalQueryExecutor(_logger);
        return await executor.ExecuteUpdateAsync<TResult>(workflowId, updateName, args);
    }

    #endregion

    /// <summary>
    /// Helper method to validate string parameters are not null or whitespace.
    /// </summary>
    private static void ValidateNotNullOrWhiteSpace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(paramName);
        }
    }
}

