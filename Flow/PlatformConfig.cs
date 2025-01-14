namespace XiansAi.Flow;

public class PlatformConfig
{
    public required string AppServerUrl { get; set; }
    public required string AppServerCertPath { get; set; }
    public required string AppServerCertPwd { get; set; }
    public required string FlowServerUrl { get; set; }
    public required string FlowServerNamespace { get; set; }
    public required string FlowServerCertPath { get; set; }
    public required string FlowServerPrivateKeyPath { get; set; }
}
