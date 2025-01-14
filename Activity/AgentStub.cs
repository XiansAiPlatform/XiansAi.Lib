using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.System;

namespace XiansAi.Activity;

public class DockerRunResult : IDisposable
{
    private readonly DockerUtil _docker;
    public DockerRunResult(DockerUtil docker) {
        _docker = docker ?? throw new ArgumentNullException(nameof(docker));
    }
    public string? Output { get; set; }

    public async void Dispose()
    {
        await _docker.Remove(true);
    }
}

public abstract class AgentStub : InstructionStub
{
    private readonly ILogger _logger;
    public AgentStub() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<AgentStub>();
        
    }

    public Agent GetAgent(int index = 1)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"[{GetType().Name}] Index must be greater than 0");
        }

        var agents = GetAgents();
        if (agents.Length < index) {
            _logger.LogError($"[{GetType().Name}] AgentAttribute.Names: [{string.Join(", ", agents)}]");
            _logger.LogError($"[{GetType().Name}] AgentAttribute.Names has less than {index} agents.", index);
            throw new InvalidOperationException($"[{GetType().Name}] AgentAttribute.Names has less than {index} agents. {string.Join(", ", agents)}");
        }
        var agentName = agents[index - 1];
        var docker = new DockerUtil(agentName);
        return new Agent(docker);
    }

    public string[] GetAgents()
    {
        var attribute = GetType().GetCustomAttribute<AgentsAttribute>();
        if (attribute == null) {
            _logger.LogError($"[{GetType().Name}] AgentAttribute is missing.");
            throw new InvalidOperationException($"[{GetType().Name}] AgentAttribute is missing.");
        }
        if (attribute.Names == null) {
            _logger.LogError($"[{GetType().Name}] AgentAttribute.Names is missing.");
            throw new InvalidOperationException($"[{GetType().Name}] AgentAttribute.Names is missing.");
        }
        return attribute.Names;
    }

    public override FlowActivity? GetCurrentActivity()
    {
        var activity = base.GetCurrentActivity();
        if (activity != null) {
            activity.AgentNames = GetAgents().ToList();
        }
        return activity;
    }

    public async Task<bool> IsPortInUse(string host, int port)
    {
        if (string.IsNullOrEmpty(host))
        {
            throw new ArgumentNullException(nameof(host));
        }
        if (port < 1 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), $"[{GetType().Name}] Port must be between 1 and 65535");
        }

        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                return true; // Port is in use
            }
        }
        catch (Exception ex) when (ex is not SocketException)
        {
            throw new InvalidOperationException($"Error checking port {port} on {host}", ex);
        }
    }

}

public class Agent  {
    private readonly DockerUtil _docker;

    public Agent(DockerUtil docker) {
        _docker = docker ?? throw new ArgumentNullException(nameof(docker));
    }

    public void SetEnv(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        _docker.SetEnvironmentVariable(key, value);
    }

    public void SetPort(int hostPort, int containerPort)
    {
        if (hostPort < 1 || hostPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(hostPort), "Port must be between 1 and 65535");
        if (containerPort < 1 || containerPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(containerPort), "Port must be between 1 and 65535");

        _docker.SetPort(hostPort.ToString(), containerPort.ToString());
    }

    public void SetVolume(string hostPath, string containerPath)
    {
        if (string.IsNullOrEmpty(hostPath))
            throw new ArgumentNullException(nameof(hostPath));
        if (string.IsNullOrEmpty(containerPath))
            throw new ArgumentNullException(nameof(containerPath));

        _docker.SetVolume(hostPath, containerPath);
    }

    public async Task<DockerRunResult> DockerRun(Dictionary<string, string>? arguments = null, bool detach = true, bool remove = false)
    {
        try 
        {
            var output = await _docker.Run(arguments, remove: remove, detach: detach);
            return new DockerRunResult(_docker) { Output = output };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to run docker container", ex);
        }
    }

    public async Task<bool> UntilHealthy(int timeoutSeconds)
    {
        return await _docker.Healthy(timeoutSeconds);
    }

}