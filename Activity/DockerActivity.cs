using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.System;

namespace XiansAi.Activity;
public abstract class DockerActivity : AgentToolActivity
{
    private readonly ILogger _logger;
    public DockerActivity() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<DockerActivity>();
        
    }

    public DockerAgentTool GetDockerAgentTool(int index = 1)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0. Provided index: " + index);
        }

        var agentTools = GetAgentTools(AgentToolType.Docker);
        if (agentTools.Count < index) {
            throw new InvalidOperationException($"Requested index {index} exceeds the number of available Docker agent tools ({agentTools.Count}). Available agent tools: {string.Join(", ", agentTools)}");
        }
        var agentToolName = agentTools[index - 1].Name;
        return new DockerAgentTool(agentToolName);
    }

}


public class DockerAgentTool : IDisposable {
    private DockerUtil _docker;
    private string _dockerImage;

    public DockerAgentTool(string agentToolName) {
        _dockerImage = agentToolName;
        _docker = new DockerUtil(agentToolName);
    }

    public DockerAgentTool Clear() {
        _docker = new DockerUtil(_dockerImage);
        return this;
    }

    public void SetEnv(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key), "Environment variable key '" + key + "' cannot be null or empty.");
        if (string.IsNullOrEmpty(value))
            throw new ArgumentNullException(nameof(value), "Environment variable value for key '" + key + "' cannot be null or empty.");

        _docker.SetEnvironmentVariable(key, value);
    }

    public void SetPort(int hostPort, int containerPort)
    {
        if (hostPort < 1 || hostPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(hostPort), "Host port must be between 1 and 65535. Provided value: " + hostPort);
        if (containerPort < 1 || containerPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(containerPort), "Container port must be between 1 and 65535. Provided value: " + containerPort);

        _docker.SetPort(hostPort.ToString(), containerPort.ToString());
    }

    public void SetVolume(string hostPath, string containerPath)
    {
        if (string.IsNullOrEmpty(hostPath))
            throw new ArgumentNullException(nameof(hostPath), "Host path cannot be null or empty.");
        if (string.IsNullOrEmpty(containerPath))
            throw new ArgumentNullException(nameof(containerPath), "Container path cannot be null or empty.");

        _docker.SetVolume(hostPath, containerPath);
    }

    public async Task<string> DockerRun(Dictionary<string, string>? arguments = null, bool detach = false, bool remove = true)
    {
        try 
        {
            var output = await _docker.Run(arguments, remove: remove, detach: detach);
            return output;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to run docker container due to an error: " + ex.Message, ex);
        }
    }

    public async Task<bool> UntilHealthy(int timeoutSeconds)
    {
        return await _docker.Healthy(timeoutSeconds);
    }

    public async void Dispose()
    {
        await _docker.Remove(true);
    }

}