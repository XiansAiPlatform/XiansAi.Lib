namespace XiansAi;

public static class PlatformConfig
{
    public static string? APP_SERVER_API_KEY = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
    public static string? APP_SERVER_URL = Environment.GetEnvironmentVariable("APP_SERVER_URL");
    public static string? FLOW_SERVER_API_KEY = Environment.GetEnvironmentVariable("FLOW_SERVER_API_KEY");
    public static string? FLOW_SERVER_URL = Environment.GetEnvironmentVariable("FLOW_SERVER_URL");
    public static string? FLOW_SERVER_NAMESPACE = Environment.GetEnvironmentVariable("FLOW_SERVER_NAMESPACE");
    public static string? OPENAI_API_KEY = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

}
