using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Activity executor for messaging operations.
/// Handles context-aware execution of message activities.
/// Eliminates duplication of Workflow.InWorkflow checks in CurrentMessage and UserMessaging.
/// </summary>
internal class MessageActivityExecutor : ContextAwareActivityExecutor<MessageActivities, MessageService>
{
    private readonly XiansAgent _agent;

    public MessageActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override MessageService CreateService()
    {
        if (_agent.HttpService == null)
        {
            throw new InvalidOperationException(
                "Message service is not available. Ensure HTTP service is configured for the agent.");
        }

        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
        return new MessageService(_agent.HttpService.Client, logger);
    }

    /// <summary>
    /// Sends a message using context-aware execution.
    /// </summary>
    public async Task SendMessageAsync(SendMessageRequest request)
    {
        await ExecuteAsync(
            act => act.SendMessageAsync(request),
            svc => svc.SendAsync(request),
            operationName: "SendMessage");
    }

    /// <summary>
    /// Gets message history using context-aware execution.
    /// </summary>
    public async Task<List<DbMessage>> GetHistoryAsync(GetMessageHistoryRequest request)
    {
        return await ExecuteAsync(
            act => act.GetMessageHistoryAsync(request),
            svc => svc.GetHistoryAsync(request),
            operationName: "GetMessageHistory");
    }

    /// <summary>
    /// Gets the last hint using context-aware execution.
    /// </summary>
    public async Task<string?> GetLastHintAsync(GetLastHintRequest request)
    {
        return await ExecuteAsync(
            act => act.GetLastHintAsync(request),
            svc => svc.GetLastHintAsync(request),
            operationName: "GetLastHint");
    }
}

