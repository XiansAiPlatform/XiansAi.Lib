using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xians.Lib.Agents.Tasks;
using Xians.Lib.Temporal;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Verifies TaskCollection's executor does not resolve a Temporal client at construction time
/// (that call is illegal inside a workflow).
///
/// dotnet test --filter "FullyQualifiedName~TaskActivityExecutorTests"
/// </summary>
[Collection("Sequential")]
public class TaskActivityExecutorTests
{
    [Fact]
    public void Constructor_DoesNotCallGetClientAsync()
    {
        var temporal = new Mock<ITemporalClientService>(MockBehavior.Strict);
        temporal.Setup(x => x.GetClientAsync()).ThrowsAsync(
            new InvalidOperationException("GetClientAsync must not run while constructing the executor"));

        var executor = new TaskActivityExecutor(
            temporal.Object,
            "test-tenant",
            NullLogger.Instance);

        Assert.NotNull(executor);
        temporal.Verify(x => x.GetClientAsync(), Times.Never);
    }
}
