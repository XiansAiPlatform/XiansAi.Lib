public static class VersionInfo
{
    public const string MajorApiVersion = "v2"; // The major version of the API that this library is designed to work with.
    public static readonly string[] CompatibleApiVersions = { "v1" }; // List of API versions that this library is compatible with.
    public const string MinimumApiVersion = "v1"; // The lowest supported API version.
    public const string Description = "Client configuration for API version negotiation and compatibility.";
}