namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// A2A tests with SystemScoped = TRUE.
/// Task queues: AgentName:WorkflowName (no tenant prefix)
/// Tenant validation: SKIPPED
/// 
/// dotnet test --filter "FullyQualifiedName~RealServerA2ASystemScopedTests"
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerA2ASystemScoped")]
public class RealServerA2ASystemScopedTests : RealServerA2ATestsBase
{
    protected override bool UseSystemScoped => true;

    public RealServerA2ASystemScopedTests() : base("A2ASystemScopedAgent")
    {
    }

    [Fact]
    public async Task SystemScoped_A2A_ChatMessage_SendHello_ReturnsHelloWorld()
    {
        await RunChatMessageTest();
    }

    [Fact]
    public async Task SystemScoped_A2A_DataMessage_SendData_ReturnsProcessedData()
    {
        await RunDataMessageTest();
    }
}
