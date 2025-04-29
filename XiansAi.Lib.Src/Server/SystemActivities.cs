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

public class SendMessageResponse {
    public required string[] MessageIds { get; set; }
}

public class SystemActivities {


    private readonly ILogger _logger;

    public SystemActivities()
    {
        _logger = Globals.LogFactory.CreateLogger<SystemActivities>();
    }

    [Activity]
    public async Task StartAndSendEventToWorkflowByType(Event evt)
    {
        _logger.LogInformation("Starting and sending event {EventType} from workflow {SourceWorkflow} to {TargetWorkflow}", 
            evt.EventType, evt.SourceWorkflowId, evt.TargetWorkflowType);

        var request = new {
            WorkflowType = evt.TargetWorkflowType,
            SignalName = Constants.EventSignalName,
            Payload = evt,
            QueueName = evt.SourceQueueName,
            Agent = evt.SourceAgent,
            Assignment = evt.SourceAssignment,
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
                evt.EventType, evt.SourceWorkflowId, evt.TargetWorkflowType);
            throw;
        }
    }

    [Activity]
    public async Task SendEventToWorkflowById(Event evt)
    {
        _logger.LogInformation("Sending event {EventType} from workflow {SourceWorkflow} to {TargetWorkflow}", 
            evt.EventType, evt.SourceWorkflowId, evt.TargetWorkflowId);

        var request = new {
            WorkflowId = evt.TargetWorkflowId,
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
                evt.EventType, evt.SourceWorkflowId, evt.TargetWorkflowId);
            throw;
        }
    }

    [Activity ("SystemActivities.GetKnowledgeAsync")]
    public async Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        var knowledgeLoader = new KnowledgeLoaderImpl();
        var knowledge = await knowledgeLoader.Load(knowledgeName);
        return knowledge ;
    }

    [Activity ("SystemActivities.RouteAsync")]
    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions options)
    {
        return await new SemanticRouterImpl().RouteAsync(messageThread, systemPrompt, capabilitiesPluginNames, options);
    }

    [Activity ("SystemActivities.HandOverThread")]
    public async Task<string?> HandOverThread(HandoverMessage message) {

        if (!SecureApi.IsReady)
        {
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/handover", message);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }


    [Activity ("SystemActivities.SendMessage")]
    public async Task<string> SendMessage(OutgoingMessage message) {

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/send", message);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }


    [Activity ("SystemActivities.GetMessageHistory")]
    public async Task<List<HistoricalMessage>> GetMessageHistory(string agent, string participantId, int page = 1, int pageSize = 10)
    {
        _logger.LogInformation("Getting message history for thread: {Agent} {ParticipantId}", agent, participantId);

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message history fetch");
            return new List<HistoricalMessage>();
        }
        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.GetAsync($"api/agent/conversation/history?agent={agent}&participantId={participantId}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var messages = await response.Content.ReadFromJsonAsync<List<HistoricalMessage>>();
            return messages ?? new List<HistoricalMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {Agent} {ParticipantId}", agent, participantId);
            throw;
        }
    }
}

public class SystemActivityOptions : ActivityOptions {
    public SystemActivityOptions() {
        StartToCloseTimeout = TimeSpan.FromSeconds(60);
        RetryPolicy = new RetryPolicy {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(10),
            MaximumAttempts = 5,
            BackoffCoefficient = 2
        };
    }
}