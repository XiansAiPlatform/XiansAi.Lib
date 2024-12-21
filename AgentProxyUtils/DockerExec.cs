using Temporalio.Activities;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

public abstract class DockerExec
{
    private readonly SystemProcess _systemProcess;
    protected string? ContainerId { get; private set; }
    protected DockerExec(string? containerId = null)
    {
        _systemProcess = new SystemProcess();
        ContainerId = containerId;
    }

    // This is abstract - derived classes MUST implement it
    protected abstract string GetImageName();

    // These are virtual - derived classes CAN override them, but don't have to
    protected virtual IDictionary<string, string>? GetEnvironmentVariables() => null;
    protected virtual IDictionary<string, string>? GetPorts() => null;
    protected virtual IDictionary<string, string>? GetCommandArgument() => null;
    protected virtual IDictionary<string, string>? GetVolumes() => null;

    protected async Task<string> Create()
    {
        var arguments = BuildDockerArguments("create");
        ContainerId = await _systemProcess.RunCommandAsync("docker", string.Join(" ", arguments));
        return ContainerId;
    }

    protected async Task<string> Start()
    {
        if (string.IsNullOrEmpty(ContainerId))
        {
            throw new InvalidOperationException("Container must be created before starting");
        }
        return await _systemProcess.RunCommandAsync("docker", $"start {ContainerId}");
    }

    protected async Task<string> Stop()
    {
        if (string.IsNullOrEmpty(ContainerId))
        {
            throw new InvalidOperationException("Container must be created before stopping");
        }
        return await _systemProcess.RunCommandAsync("docker", $"stop {ContainerId}");
    }

    protected async Task<string> Remove()
    {
        if (string.IsNullOrEmpty(ContainerId))
        {
            throw new InvalidOperationException("Container must be created before removing");
        }
        var result = await _systemProcess.RunCommandAsync("docker", $"rm {ContainerId}");
        ContainerId = null;
        return result;
    }

    protected async Task<string> Run()
    {
        var arguments = BuildDockerArguments("run", includeRmFlag: true);
        return await _systemProcess.RunCommandAsync("docker", string.Join(" ", arguments));
    }

    private List<string> BuildDockerArguments(string command, bool includeRmFlag = false)
    {
        var image = GetImageName();
        var environmentVariables = GetEnvironmentVariables();
        var ports = GetPorts();
        var volumes = GetVolumes();
        var commandArguments = GetCommandArgument();
        
        var arguments = new List<string> { command };
        
        if (includeRmFlag)
        {
            arguments.Add("--rm");
        }

        // Add environment variables
        if (environmentVariables != null)
        {
            foreach (var env in environmentVariables)
            {
                arguments.Add("-e");
                arguments.Add($"{env.Key}={env.Value}");
            }
        }

        // Add port mappings
        if (ports != null)
        {
            foreach (var port in ports)
            {
                arguments.Add("-p");
                arguments.Add($"{port.Value}:{port.Key}");
            }
        }

        // Add volumes
        if (volumes != null)
        {
            foreach (var volume in volumes)
            {
                arguments.Add("-v");
                arguments.Add($"{volume.Value}:{volume.Key}");
            }
        }

        // Add image and command parameters
        arguments.Add(image);

        // Add command arguments
        if (commandArguments != null)
        {
            arguments.AddRange(commandArguments.Select(param => $"--{param.Key} \"{param.Value}\""));
        }

        return arguments;
    }
}

