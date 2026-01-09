namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// A2A tests with SystemScoped = FALSE.
/// Task queues: {tenantId}:AgentName:WorkflowName (WITH tenant prefix)
/// Tenant validation: ENFORCED
/// 
/// CRITICAL: Must use agent.Options.CertificateTenantId when starting workflows!
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerA2ATenantScopedOnlyTests"
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerA2ATenantScopedOnly")]
public class RealServerA2ATenantScopedOnlyTests : RealServerA2ATestsBase
{
    protected override bool UseSystemScoped => false;

    public RealServerA2ATenantScopedOnlyTests() : base("A2ATenantScopedAgent")
    {
    }

    [Fact]
    public async Task TenantScoped_A2A_ChatMessage_SendHello_ReturnsHelloWorld()
    {
        await RunChatMessageTest();
    }

    [Fact]
    public async Task TenantScoped_A2A_DataMessage_SendData_ReturnsProcessedData()
    {
        await RunDataMessageTest();
    }
}
