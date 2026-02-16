using Xians.Lib.Tests.Scripts;

namespace Xians.Lib.Tests.Scripts;

/// <summary>
/// Test to run the DeleteAllSchedules script.
/// Execute with: dotnet test --filter "FullyQualifiedName~DeleteAllSchedules_Run"
/// RUN_SCRIPTS=true dotnet test  --filter "FullyQualifiedName~DeleteAllSchedules_Run" --no-build
/// </summary>
[Trait("Category", "Script")]
public class DeleteAllSchedulesTests
{
    [Fact]
    public async Task DeleteAllSchedules_Run()
    {
        var runIntegrationTests = bool.TryParse(
            Environment.GetEnvironmentVariable("RUN_SCRIPTS"),
            out var shouldRun) && shouldRun;

        if (!runIntegrationTests)
        {
            Console.WriteLine("Skipped: Set RUN_SCRIPTS=true to run");
            return;
        }

        await DeleteAllSchedules.RunAsync();
    }
}
