using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using XiansAi.Flow;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Models;
using XiansAi.Router;

public class SendMessageResponse {
    public required string MessageId { get; set; }
    public required string ThreadId { get; set; }
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
    public async Task<SendMessageResponse?> SendMessage(OutgoingMessage message) {
        _logger.LogInformation("Sending message: {Message}", message);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            return null;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/conversation/outbound", message);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw;
        }
    }
}