public static class VersionInfo
{
    public const string LibVersion = "1.0.0";
    public const string MajorApiVersion = "v1";
    public static readonly string[] CompatibleApiVersions = { "v1" };
    public const string MinApiVersion = "v1";
    public const bool AutoVersioning = true;
    public const string Description = "Client configuration for API version negotiation and compatibility.";
}