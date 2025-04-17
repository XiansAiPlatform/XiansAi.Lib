using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using Temporalio.Activities;
using XiansAi.Flow;

public class SystemActivities {
    private readonly ILogger _logger;

    public SystemActivities()
    {
        _logger = Globals.LogFactory.CreateLogger<SystemActivities>();
    }

    [Activity]
    public async Task<List<IncomingMessage>> GetMessageHistory(string threadId, int page = 1, int pageSize = 10) {
        _logger.LogInformation("Getting message history for thread: {ThreadId}", threadId);
        return new List<IncomingMessage>();
    }

    [Activity]
    public async Task<bool> SendMessage(OutgoingMessage message) {
        _logger.LogInformation("Sending message: {Message}", message);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message send operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.PostAsJsonAsync("api/agent/messaging/outbound", message);
            response.EnsureSuccessStatusCode();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {Message}", message);
            throw;
        }
    }
}