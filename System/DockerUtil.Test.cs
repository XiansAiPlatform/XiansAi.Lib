using Xunit;

namespace XiansAi.System;

public class DockerUtilTests
{

    /*
      dotnet test --filter "FullyQualifiedName~DockerUtil_HappyPath_Test"
      */
    [Fact]
    public async Task DockerUtil_HappyPath_Test()
    {
        var dockerUtil = new DockerUtil("flowmaxer/scraper-agent");

        // Act
        var containerId = await dockerUtil.Run();
        Console.WriteLine($"Container ID: {containerId}");
        var isHealthy = await dockerUtil.Healthy(60, 5);
        Console.WriteLine($"Is Healthy: {isHealthy}");
        var removeResult = await dockerUtil.Remove(true);
        Console.WriteLine($"Remove Result: {removeResult}");

        // Assert
        Assert.NotEmpty(containerId);
        Assert.True(isHealthy);
        Assert.NotEmpty(removeResult);
    }
}