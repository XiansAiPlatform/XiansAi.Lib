using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Server;
using XiansAi.Flow;

public class ThreadHistoryService
{
    private readonly ILogger _logger;

    public ThreadHistoryService()
    {
        _logger = Globals.LogFactory.CreateLogger<ThreadHistoryService>();
    }

    public async Task<List<HistoricalMessage>> GetMessageHistory(string threadId, int page = 1, int pageSize = 10)
    {
        _logger.LogInformation("Getting message history for thread: {ThreadId}", threadId);

        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping message history fetch");
            return new List<HistoricalMessage>();
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var response = await client.GetAsync($"api/agent/conversation/history?threadId={threadId}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var messages = await response.Content.ReadFromJsonAsync<List<HistoricalMessage>>();
            return messages ?? new List<HistoricalMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching message history for thread: {ThreadId}", threadId);
            throw;
        }
    }

}