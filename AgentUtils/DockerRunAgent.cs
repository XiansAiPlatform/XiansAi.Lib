using System.Net.Sockets;
using System.Reflection;

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

public abstract class DockerRunAgent : IDisposable
{
    private readonly DockerUtil _docker;
    private readonly string? _settingsFile;
    public DockerRunAgent() {
        var dockerImage = GetType().GetCustomAttribute<AgentAttribute>();
        if (dockerImage == null) {
            throw new InvalidOperationException("DockerImageAttribute is missing.");
        }
        _docker = new DockerUtil(dockerImage.Name);
        _settingsFile = dockerImage.Settings;
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

    public void SetEnvironmentVariable(string key, string value)
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

    public async Task<DockerRunResult> DockerRun(Dictionary<string, string>? arguments = null, bool detach = true)
    {
        var output = await _docker.Run(arguments, remove: false, detach: detach);
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

    protected string LoadSettings()
    {
        if (_settingsFile == null) {
            throw new InvalidOperationException("Settings file is not set");
        }
        var settings = File.ReadAllText(_settingsFile);
        return settings;
    }
}