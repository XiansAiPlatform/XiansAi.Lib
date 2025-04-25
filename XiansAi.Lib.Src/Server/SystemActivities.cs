using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;
using XiansAi.Events;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Models;
using XiansAi.Router;

public class SendMessageResponse
{
    public required string[] MessageIds { get; set; }
}

public class SystemActivities
{


    private readonly ILogger _logger;

    public SystemActivities()
    {
        _logger = Globals.LogFactory.CreateLogger<SystemActivities>();
    }

    [Activity]
    public async Task StartAndSendEventToWorkflowByType(StartAndSendEvent evt)
    {
        _logger.LogInformation("Starting and sending event {EventType} from workflow {SourceWorkflow} to {TargetWorkflow}",
            evt.eventType, evt.sourceWorkflowId, evt.targetWorkflowType);

        var request = new
        {
            WorkflowType = evt.targetWorkflowType,
            SignalName = Constants.EventSignalName,
            Payload = evt,
            QueueName = evt.sourceQueueName,
            Agent = evt.sourceAgent,
            Assignment = evt.sourceAssignment,
        };

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/signal/with-start", request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start and send event {EventType} from {SourceWorkflow} to {TargetWorkflow}",
                evt.eventType, evt.sourceWorkflowId, evt.targetWorkflowType);
            throw;
        }
    }

    [Activity]
    public async Task SendEventToWorkflowById(Event evt)
    {
        _logger.LogInformation("Sending event {EventType} from workflow {SourceWorkflow} to {TargetWorkflow}",
            evt.eventType, evt.sourceWorkflowId, evt.targetWorkflowId);


        _logger.LogInformation("Event payload: {Payload}", JsonSerializer.Serialize(evt));


        var request = new
        {
            WorkflowId = evt.targetWorkflowId,
            SignalName = Constants.EventSignalName,
            Payload = evt
        };
        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/signal", request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {EventType} from {SourceWorkflow} to {TargetWorkflow}",
                evt.eventType, evt.sourceWorkflowId, evt.targetWorkflowId);
            throw;
        }
    }

    [Activity("SystemActivities.GetKnowledgeAsync")]
    public async Task<Knowledge> GetKnowledgeAsync(string knowledgeName)
    {
        return await new KnowledgeManagerImpl().GetKnowledgeAsync(knowledgeName);
    }

    [Activity("SystemActivities.RouteAsync")]
    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options)
    {
        return await new SemanticRouterImpl().RouteAsync(messageThread, systemPrompt, capabilitiesPluginNames, options);
    }

    [Activity("SystemActivities.HandOverMessage")]
    public async Task<SendMessageResponse> HandOverMessage(HandoverMessage message)
    {
        _logger.LogInformation("Handing over message: {Message}", JsonSerializer.Serialize(message));
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        if (string.IsNullOrEmpty(message.ChildWorkflowId))
        {
            throw new Exception("Child workflow id is required for handover");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/handover", message);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SendMessageResponse>() ?? throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    [Activity("SystemActivities.StartAndHandoverMessage")]
    public async Task<SendMessageResponse> StartAndHandoverMessage(StartAndHandoverMessage message)
    {
        _logger.LogInformation("Starting and handing over message: {Message}", message);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        if (string.IsNullOrEmpty(message.WorkflowTypeToStart))
        {
            throw new Exception("Workflow type is required");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/handover", message);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Handover response: {Response}", await response.Content.ReadAsStringAsync());

            return await response.Content.ReadFromJsonAsync<SendMessageResponse>() ?? throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }

    [Activity("SystemActivities.SendMessage")]
    public async Task<SendMessageResponse> SendMessage(OutgoingMessage message)
    {
        _logger.LogInformation("Sending message: {Message}", JsonSerializer.Serialize(message));
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/send", message);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SendMessageResponse>() ?? throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }


    [Activity("SystemActivities.SendHandOverResponse")]
    public async Task<SendMessageResponse> SendHandOverResponse(HandoverMessage message)
    {
        _logger.LogInformation("Sending handover response: {Message}", JsonSerializer.Serialize(message));
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/handover-response", message);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SendMessageResponse>() ?? throw new Exception($"Failed to parse response {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }
}

public class SystemActivityOptions : ActivityOptions
{
    public SystemActivityOptions()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(60);
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(10),
            MaximumAttempts = 5,
            BackoffCoefficient = 2
        };
    }
}