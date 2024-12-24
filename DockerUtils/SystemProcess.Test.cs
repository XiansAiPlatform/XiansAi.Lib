using Xunit;

public class SystemProcessTest
{
    /*
   dotnet test --filter "FullyQualifiedName~SystemProcessTest.ExecuteCommand_ShouldReturnSuccess_WhenCommandIsValid"

    */
    [Fact]
    public async Task ExecuteCommand_ShouldReturnSuccess_WhenCommandIsValid()
    {

        var containerId = await new SystemProcess().RunCommandAsync("docker", "run -d flowmaxer/scraper-agent");

        Console.WriteLine($"Container ID: {containerId}");

        // Arrange
        var command = "inspect --format={{.State.Health.Status}} " + containerId.Trim();

        Console.WriteLine($"Command: {command}");
        var expectedOutput = "healthy";


        // Act
        var result = await new SystemProcess().RunCommandAsync("docker", command);

        Assert.Equal(expectedOutput, result.Trim());
    }
}
