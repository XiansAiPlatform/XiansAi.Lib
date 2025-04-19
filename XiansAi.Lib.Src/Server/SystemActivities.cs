using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;
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

    [Activity ("SystemActivities.GetKnowledgeAsync")]
    public async Task<Knowledge> GetKnowledgeAsync(string knowledgeName)
    {
        return await new KnowledgeManagerImpl().GetKnowledgeAsync(knowledgeName);
    }

    [Activity ("SystemActivities.RouteAsync")]
    public async Task<string> RouteAsync(MessageThread messageThread, string systemPrompt, string[] capabilitiesPluginNames, RouterOptions? options)
    {
        return await new SemanticRouterImpl().RouteAsync(messageThread, systemPrompt, capabilitiesPluginNames, options);
    }


    [Activity ("SystemActivities.SendMessage")]
    public async Task<SendMessageResponse> SendMessage(OutgoingMessage message) {
        _logger.LogInformation("Sending message: {Message}", message);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            throw new Exception("App server secure API is not ready, skipping message send operation");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound", message);
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