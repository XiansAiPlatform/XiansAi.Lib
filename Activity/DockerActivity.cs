using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.System;

namespace XiansAi.Activity;
public abstract class DockerActivity : AgentActivity
{
    private readonly ILogger _logger;
    public DockerActivity() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<DockerActivity>();
        
    }

    public DockerAgent GetDockerAgent(int index = 1)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0. Provided index: " + index);
        }

        var agents = GetAgents(AgentType.Docker);
        if (agents.Count < index) {
            throw new InvalidOperationException($"Requested index {index} exceeds the number of available Docker agents ({agents.Count}). Available agents: {string.Join(", ", agents)}");
        }
        var agentName = agents[index - 1].Name;
        return new DockerAgent(agentName);
    }

}


public class DockerAgent : IDisposable {
    private readonly DockerUtil _docker;

    public DockerAgent(string agentName) {
        _docker = new DockerUtil(agentName);
    }

    public void SetEnv(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key), "Environment variable key cannot be null or empty.");
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Environment variable value cannot be null.");

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