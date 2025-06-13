namespace XiansAi;

public static class PlatformConfig
{
    public static string? APP_SERVER_API_KEY = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
    public static string? APP_SERVER_URL = Environment.GetEnvironmentVariable("APP_SERVER_URL");
    public static string? KEYCLOAK_JWKS_URL = Environment.GetEnvironmentVariable("KEYCLOAK_JWKS_URL");
    public static string? KEYCLOAK_ISSUER = Environment.GetEnvironmentVariable("KEYCLOAK_ISSUER");
    public static string? KEYCLOAK_CLIENT_ID = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_ID");
}
