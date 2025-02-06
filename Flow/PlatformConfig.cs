namespace XiansAi.Flow;

public static class PlatformConfig
{
    public static string? APP_SERVER_API_KEY = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
    public static string? APP_SERVER_URL = Environment.GetEnvironmentVariable("APP_SERVER_URL");
    public static string? APP_SERVER_CERT_PATH = Environment.GetEnvironmentVariable("APP_SERVER_CERT_PATH");
    public static string? APP_SERVER_CERT_PWD = Environment.GetEnvironmentVariable("APP_SERVER_CERT_PWD");
    
    public static string? FLOW_SERVER_API_KEY = Environment.GetEnvironmentVariable("FLOW_SERVER_API_KEY");
    public static string? FLOW_SERVER_URL = Environment.GetEnvironmentVariable("FLOW_SERVER_URL");
    public static string? FLOW_SERVER_NAMESPACE = Environment.GetEnvironmentVariable("FLOW_SERVER_NAMESPACE");
    public static string? FLOW_SERVER_CERT_PATH = Environment.GetEnvironmentVariable("FLOW_SERVER_CERT_PATH");
    public static string? FLOW_SERVER_PRIVATE_KEY_PATH = Environment.GetEnvironmentVariable("FLOW_SERVER_PRIVATE_KEY_PATH");

}
