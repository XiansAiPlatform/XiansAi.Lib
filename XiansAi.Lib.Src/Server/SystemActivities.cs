using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using XiansAi.Flow;

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

    [Activity]
    public async Task<List<IncomingMessage>> GetMessageHistory(string threadId, int page = 1, int pageSize = 10) {
        _logger.LogInformation("Getting message history for thread: {ThreadId}", threadId);
        
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message history fetch");
            return new List<IncomingMessage>();
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.GetAsync($"api/agent/conversation/history?threadId={threadId}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();
            
            var messages = await response.Content.ReadFromJsonAsync<List<IncomingMessage>>();
            return messages ?? new List<IncomingMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {ThreadId}", threadId);
            throw;
        }
    }

    [Activity]
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