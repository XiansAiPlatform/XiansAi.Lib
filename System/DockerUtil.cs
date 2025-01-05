using System.Diagnostics;

namespace XiansAi.System;

public class DockerUtil
{
    private readonly ISystemProcess _systemProcess;
    private readonly string _dockerImage;
    private string? _containerId;

    private readonly Dictionary<string, string> _environmentVariables = new();
    private readonly Dictionary<string, string> _ports = new();
    private readonly Dictionary<string, string> _volumes = new();
    private Dictionary<string, string> _commandArguments = new();

    public DockerUtil SetEnvironmentVariable(string key, string value)
    {
        _environmentVariables[key] = value;
        return this;
    }

    public DockerUtil SetPort(string hostPort, string containerPort)
    {
        _ports[hostPort] = containerPort;
        return this;
    }

    public DockerUtil SetVolume(string hostPath, string containerPath)
    {
        _volumes[hostPath] = containerPath;
        return this;
    }


    public DockerUtil(string dockerImage)
    {
        _dockerImage = dockerImage;
        _systemProcess = new SystemProcess();
    }

    public async Task<string> Create()
    {
        var arguments = BuildDockerArguments("create");
        _containerId = await _systemProcess.RunCommandAsync("docker", string.Join(" ", arguments));
        return _containerId.Trim();
    }

    public async Task<string> Start()
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            throw new InvalidOperationException("Container must be created before starting");
        }
        var result = await _systemProcess.RunCommandAsync("docker", $"start {_containerId.Trim()}");
        return result.Trim();
    }

    public async Task<string> Stop()
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            throw new InvalidOperationException("Container must be created before stopping");
        }
        var result = await _systemProcess.RunCommandAsync("docker", $"stop {_containerId.Trim()}");
        return result.Trim();
    }

    public async Task<string> Remove(bool force = false)
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            throw new InvalidOperationException("Container must be created before removing");
        }
        var result = await _systemProcess.RunCommandAsync("docker", $"rm {(force ? "-f" : "")} {_containerId.Trim()}");
        _containerId = null;
        return result.Trim();
    }

    public async Task<string> Run(Dictionary<string, string>? args = null, bool remove = false, bool detach = true)
    {
        if (args != null)
        {
            _commandArguments = args;
        }
        var arguments = BuildDockerArguments("run", includeRmFlag: remove, detach: detach);
        _containerId = await _systemProcess.RunCommandAsync("docker", string.Join(" ", arguments));
        return _containerId.Trim();
    }

    public async Task<bool> Healthy(int timeoutSeconds = 30, int intervalSeconds = 5)
    {
        if (string.IsNullOrEmpty(_containerId))
        {
            throw new InvalidOperationException("Container must be created before checking health");
        }

        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var result = await _systemProcess.RunCommandAsync("docker", $"inspect --format='{{{{.State.Health.Status}}}}' {_containerId.Trim()}");
            if (result.Trim().Equals("'healthy'"))
            {
                return true;
            }
            await Task.Delay(interval);
        }

        return false;
    }   

    private List<string> BuildDockerArguments(string command, bool includeRmFlag = false, bool detach = true)
    {
        
        var arguments = new List<string> { command };
        
        if (includeRmFlag)
        {
            arguments.Add("--rm");
        }

        if (detach)
        {
            arguments.Add("--detach");
        }

        // Add environment variables
        if (_environmentVariables != null)
        {
            foreach (var env in _environmentVariables)
            {
                arguments.Add("-e");
                arguments.Add($"{env.Key}={env.Value}");
            }
        }

        // Add port mappings
        if (_ports != null)
        {
            foreach (var port in _ports)
            {
                arguments.Add("-p");
                arguments.Add($"{port.Value}:{port.Key}");
            }
        }

        // Add volumes
        if (_volumes != null)
        {
            foreach (var volume in _volumes)
            {
                arguments.Add("-v");
                arguments.Add($"{volume.Value}:{volume.Key}");
            }
        }

        // Add image and command parameters
        arguments.Add(_dockerImage);

        // Add command arguments
        if (_commandArguments != null)
        {
            arguments.AddRange(_commandArguments.Select(param => $"--{param.Key} \"{param.Value}\""));
        }

        return arguments;
    }
}
