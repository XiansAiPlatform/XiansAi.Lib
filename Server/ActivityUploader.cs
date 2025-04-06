using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using XiansAi.Http;
using XiansAi.Models;

namespace XiansAi.Server;

public class ActivityUploader
{
    private readonly ILogger _logger;

    public ActivityUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<ActivityUploader>();
    }

    public async Task UploadActivity(FlowActivityHistory activityHistory)
    {
        _logger.LogInformation("Uploading activity to server: {activity}", activityHistory);
        if (SecureApi.IsReady())
        {
            HttpClient client = SecureApi.GetClient();

            var response = await client.PostAsync("api/agent/activity-history", JsonContent.Create(activityHistory));
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("App server secure API is not ready, skipping activity upload to server");
        }
    }
}
