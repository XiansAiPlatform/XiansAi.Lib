namespace XiansAi;

public static class PlatformConfig
{
    public static string? APP_SERVER_API_KEY = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
    public static string? APP_SERVER_URL = Environment.GetEnvironmentVariable("APP_SERVER_URL");

}
