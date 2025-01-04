using DotNetEnv;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
public class ActivityUploader
{
    private readonly ILogger _logger;

    public ActivityUploader()
    {
        Env.Load();
        _logger = Globals.LogFactory.CreateLogger<ActivityUploader>();
    }

    public async Task UploadActivity(Activity activity)
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
