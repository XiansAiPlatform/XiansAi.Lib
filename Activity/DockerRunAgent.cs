using System.Net.Sockets;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.System;

namespace XiansAi.Activity;

public class DockerRunResult : IDisposable
{
    private readonly DockerUtil _docker;
    public DockerRunResult(DockerUtil docker) {
        _docker = docker;
    }
    public string? Output { get; set; }

    public async void Dispose()
    {
        await _docker.Remove(true);
    }
}

public abstract class DockerRunAgent : InstructionAgent, IDisposable
{
    private readonly DockerUtil _docker;
    private readonly ILogger _logger;
    public DockerRunAgent() : base()
    {
        _logger = Globals.LogFactory.CreateLogger<DockerRunAgent>();
        _docker = new DockerUtil(GetDockerImageName());
    }

    public string GetDockerImageName()
    {
        var attribute = GetType().GetCustomAttribute<DockerImageAttribute>();
        if (attribute == null) {
            throw new InvalidOperationException("DockerImageAttribute is missing.");
        }
        if (attribute.Name == null) {
            throw new InvalidOperationException("DockerImageAttribute.Name is missing.");
        }
        return attribute.Name;
    }

    public override Models.Activity GetCurrentActivity()
    {
        var activity = base.GetCurrentActivity();
        activity.AgentName = GetDockerImageName();
        return activity;
    }

    public static async Task<bool> IsPortInUse(string host, int port)
    {
        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                return true; // Port is in use
            }
        }
        catch (SocketException)
        {
            return false; // Port is not in use
        }
    }

    public void SetEnv(string key, string value)
    {
        _docker.SetEnvironmentVariable(key, value);
    }

    public void SetPort(int hostPort, int containerPort)
    {
        _docker.SetPort(hostPort.ToString(), containerPort.ToString());
    }

    public void SetVolume(string hostPath, string containerPath)
    {
        _docker.SetVolume(hostPath, containerPath);
    }

    public async Task<DockerRunResult> DockerRun(Dictionary<string, string>? arguments = null, bool detach = true, bool remove = false)
    {
        var output = await _docker.Run(arguments, remove: remove, detach: detach);
        return new DockerRunResult(_docker) { Output = output };
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