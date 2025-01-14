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

    public async Task UploadActivity(FlowActivity activity)
    {
        _logger.LogInformation("Uploading activity to server: {activity}", activity);
        if (SecureApi.IsReady())
        {
            HttpClient client = SecureApi.GetClient();

            var response = await client.PostAsync("api/server/activities", JsonContent.Create(activity));
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("App server secure API is not ready, skipping activity upload to server");
        }
    }
}
