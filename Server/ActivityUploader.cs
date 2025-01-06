using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using XiansAi.Http;

namespace XiansAi.Server;

public class ActivityUploader
{
    private readonly ILogger _logger;

    public ActivityUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<ActivityUploader>();
    }

    public async Task UploadActivity(XiansAi.Models.Activity activity)
    {
        if (SecureApi.IsReady())
        {
            HttpClient client = SecureApi.GetClient();

            var response = await client.PostAsync("api/server/activities", JsonContent.Create(activity));
            response.EnsureSuccessStatusCode();
        }
        else
        {
            _logger.LogWarning("SecureApi is not ready, skipping activity upload to server");
        }
    }
}
