using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using XiansAi.Models;

namespace Server;

public class ActivityUploader
{
    private readonly ILogger _logger;

    public ActivityUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<ActivityUploader>();
    }

    public async Task UploadActivity(ActivityHistory activityHistory)
    {
        if (SecureApi.IsReady)
        {
            var client = SecureApi.Instance.Client;

            var response = await client.PostAsync("api/agent/activity-history", JsonContent.Create(activityHistory));
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("App server secure API is not ready, skipping activity upload to server");
        }
    }
}
