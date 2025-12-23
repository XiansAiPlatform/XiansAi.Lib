namespace Xians.Lib.Common.Models;

/// <summary>
/// Server-provided settings for Temporal/Flow server configuration.
/// </summary>
public class ServerSettings
{
    public required string FlowServerUrl { get; set; }
    public required string FlowServerNamespace { get; set; }
    public string? FlowServerCertBase64 { get; set; }
    public string? FlowServerPrivateKeyBase64 { get; set; }
}


