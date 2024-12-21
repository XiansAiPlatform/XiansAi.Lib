using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
public class TemporalConfig
{
    public TemporalConfig()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var config = configuration.GetSection("Temporal");

        TemporalServerUrl = config["TemporalServerUrl"] ?? throw new InvalidOperationException("TemporalServerUrl is required.");
        Namespace = config["Namespace"] ?? throw new InvalidOperationException("Namespace is required.");
        ClientCert = config["ClientCert"] ?? throw new InvalidOperationException("ClientCert is required.");
        ClientPrivateKey = config["ClientPrivateKey"] ?? throw new InvalidOperationException("ClientPrivateKey is required.");
        TaskQueue = config["TaskQueue"] ?? throw new InvalidOperationException("TaskQueue is required.");
    }

    [Required]
    public string TemporalServerUrl { get; set; }

    [Required]
    public string Namespace { get; set; }

    [Required]
    public string ClientCert { get; set; }

    [Required]
    public string ClientPrivateKey { get; set; }


    [Required]
    public string TaskQueue { get; set; }

}