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

    [Activity("System Activities: Send Event")]
    public async Task SendEvent(EventDto eventDto)
    {
        _logger.LogInformation("Sending event {EventType} from workflow {SourceWorkflow} to {TargetWorkflow}", 
            eventDto.EventType, eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);

        var request = new {
            WorkflowType = eventDto.TargetWorkflowType,
            WorkflowId = eventDto.TargetWorkflowId,
            SignalName = Constants.EventSignalName,
            eventDto.Payload,
            QueueName = eventDto.SourceQueueName,
            Agent = eventDto.SourceAgent,
            Assignment = eventDto.SourceAssignment,
        };
        
        try
        {
            if (!SecureApi.IsReady)
            {
                throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
            }

            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/signal/with-start", request);
            response.EnsureSuccessStatusCode();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping event send operation.");
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            _logger.LogError(ex, "Failed to start and send event {EventType} from {SourceWorkflow} to {TargetWorkflow}", 
                eventDto.EventType, eventDto.SourceWorkflowId, eventDto.TargetWorkflowType);
            throw;
        }
    }


    [Activity ("System Activities: Get Knowledge")]
    public async Task<Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        try {
            var knowledgeLoader = new KnowledgeLoaderImpl();
            var knowledge = await knowledgeLoader.Load(knowledgeName);
            return knowledge ;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge: {KnowledgeName}", knowledgeName);
            throw;
        }
    }

    [Activity ("System Activities: Route Message")]
    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, AgentContext agentContext, RouterOptions options)
    {
        // To improve performance, we set the agent context explicitly here
        AgentContext.SetExplicitInstance(agentContext);
        // do the routing
        return await new SemanticRouterImpl().RouteAsync(messageThread, systemPrompt, capabilitiesPluginNames, options);
    }

    [Activity ("System Activities: Hand Over message Thread")]
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


    [Activity ("System Activities: Send Message")]
    public async Task<string> SendMessage(OutgoingMessage message) {

        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new InvalidOperationException("SecureApi is not ready. Please ensure it is properly initialized.");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound/send", message);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message send operation.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw new Exception($"Failed to send message: {ex.Message}");
        }
    }


    [Activity ("System Activities: Get Message Thread History")]
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
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed. Skipping message history fetch.");
            return new List<HistoricalMessage>();
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