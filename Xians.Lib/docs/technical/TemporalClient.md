# Temporal Client Guide

Comprehensive guide for using the Temporal client service in Xians.Lib.

## Overview

The Temporal client service provides:
- **Lazy connection** - Connects only when needed
- **Automatic retry** - Recovers from transient connection failures
- **mTLS support** - Secure connections with client certificates
- **Health monitoring** - Detects and recovers from connection issues
- **Graceful cleanup** - Proper disconnect and disposal

## Basic Usage

### Creating the Service

```csharp
using Xians.Lib.Configuration;
using Xians.Lib.Temporal;
using Xians.Lib.Common;

var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default"
};

using var temporalService = ServiceFactory.CreateTemporalClientService(config);
```

### Getting the Client

The client connects automatically on first use:

```csharp
// Get the Temporal client (connects if not already connected)
var client = await temporalService.GetClientAsync();

// Now use the client for workflow operations
var workflowHandle = await client.StartWorkflowAsync(
    (MyWorkflow wf) => wf.RunAsync(),
    new(id: "my-workflow-id", taskQueue: "my-task-queue")
);
```

## Secure Connections (mTLS)

### Using TLS Certificates

For production environments, use mTLS:

```csharp
// Load certificates
var certBytes = File.ReadAllBytes("client-cert.pem");
var keyBytes = File.ReadAllBytes("client-key.pem");

var config = new TemporalConfiguration
{
    ServerUrl = "temporal.example.com:7233",
    Namespace = "production",
    CertificateBase64 = Convert.ToBase64String(certBytes),
    PrivateKeyBase64 = Convert.ToBase64String(keyBytes)
};

using var temporalService = ServiceFactory.CreateTemporalClientService(config);
var client = await temporalService.GetClientAsync();
```

### From Environment Variables

```csharp
// Set environment variables
Environment.SetEnvironmentVariable("TEMPORAL_SERVER_URL", "temporal.example.com:7233");
Environment.SetEnvironmentVariable("TEMPORAL_NAMESPACE", "production");
Environment.SetEnvironmentVariable("TEMPORAL_CERT_BASE64", certBase64);
Environment.SetEnvironmentVariable("TEMPORAL_KEY_BASE64", keyBase64);

// Create from environment
using var temporalService = ServiceFactory.CreateTemporalClientServiceFromEnvironment();
```

## Advanced Features

### Health Monitoring

Check connection health:

```csharp
bool isHealthy = temporalService.IsConnectionHealthy();

if (!isHealthy)
{
    Console.WriteLine("Connection unhealthy, will reconnect automatically");
}
```

### Manual Reconnection

Force a reconnection:

```csharp
await temporalService.ForceReconnectAsync();
Console.WriteLine("Forced reconnection to Temporal server");

// Next GetClientAsync call will create a new connection
var client = await temporalService.GetClientAsync();
```

### Graceful Disconnect

Disconnect when done:

```csharp
await temporalService.DisconnectAsync();
Console.WriteLine("Disconnected from Temporal server");

// Dispose also disconnects automatically
temporalService.Dispose();
```

## Connection Retry

The service automatically retries connection failures:

```csharp
var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default",
    
    // Customize retry behavior
    MaxRetryAttempts = 5,
    RetryDelaySeconds = 10
};

using var temporalService = ServiceFactory.CreateTemporalClientService(config);

// Will retry up to 5 times with 10 second delays between attempts
var client = await temporalService.GetClientAsync();
```

## Working with Workflows

### Starting a Workflow

```csharp
var client = await temporalService.GetClientAsync();

var handle = await client.StartWorkflowAsync(
    (MyWorkflow wf) => wf.RunAsync(input),
    new WorkflowOptions
    {
        Id = "workflow-123",
        TaskQueue = "my-queue",
        WorkflowExecutionTimeout = TimeSpan.FromHours(1)
    }
);

Console.WriteLine($"Started workflow: {handle.Id}");
```

### Querying a Workflow

```csharp
var client = await temporalService.GetClientAsync();

var handle = client.GetWorkflowHandle<MyWorkflow>("workflow-123");
var status = await handle.QueryAsync(wf => wf.GetStatusAsync());

Console.WriteLine($"Workflow status: {status}");
```

### Signaling a Workflow

```csharp
var client = await temporalService.GetClientAsync();

var handle = client.GetWorkflowHandle("workflow-123");
await handle.SignalAsync("approval-signal", new ApprovalData { Approved = true });
```

### Waiting for Workflow Result

```csharp
var client = await temporalService.GetClientAsync();

var handle = await client.StartWorkflowAsync(
    (MyWorkflow wf) => wf.RunAsync(input),
    new WorkflowOptions { Id = "workflow-123", TaskQueue = "my-queue" }
);

// Wait for completion
var result = await handle.GetResultAsync();
Console.WriteLine($"Workflow result: {result}");
```

## Error Handling

### Connection Errors

```csharp
try
{
    var client = await temporalService.GetClientAsync();
}
catch (InvalidOperationException ex)
{
    // All retry attempts failed
    Console.WriteLine($"Failed to connect to Temporal: {ex.Message}");
    
    // Check inner exception for details
    Console.WriteLine($"Details: {ex.InnerException?.Message}");
}
```

### Workflow Errors

```csharp
var client = await temporalService.GetClientAsync();

try
{
    var handle = await client.StartWorkflowAsync(...);
    var result = await handle.GetResultAsync();
}
catch (Temporalio.Exceptions.WorkflowFailedException ex)
{
    Console.WriteLine($"Workflow failed: {ex.Message}");
}
catch (Temporalio.Exceptions.WorkflowCanceledException ex)
{
    Console.WriteLine($"Workflow was cancelled: {ex.Message}");
}
```

## Configuration Best Practices

### Development Environment

```csharp
var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default",
    // No TLS for local development
    MaxRetryAttempts = 3,
    RetryDelaySeconds = 2
};
```

### Production Environment

```csharp
var config = new TemporalConfiguration
{
    ServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL")!,
    Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE")!,
    CertificateBase64 = Environment.GetEnvironmentVariable("TEMPORAL_CERT_BASE64"),
    PrivateKeyBase64 = Environment.GetEnvironmentVariable("TEMPORAL_KEY_BASE64"),
    
    // More aggressive retry for production
    MaxRetryAttempts = 5,
    RetryDelaySeconds = 10
};
```

## Thread Safety

The Temporal client service is **fully thread-safe**:

```csharp
var temporalService = ServiceFactory.CreateTemporalClientService(config);

// Use from multiple threads safely
var tasks = Enumerable.Range(0, 10).Select(async i =>
{
    var client = await temporalService.GetClientAsync();
    return await client.StartWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(i),
        new WorkflowOptions { Id = $"workflow-{i}", TaskQueue = "my-queue" }
    );
});

var handles = await Task.WhenAll(tasks);
```

## Lifecycle Management

### Singleton Pattern

Create once, use throughout application:

```csharp
public class TemporalService
{
    private static readonly ITemporalClientService _temporalService =
        ServiceFactory.CreateTemporalClientServiceFromEnvironment();
    
    public static async Task<ITemporalClient> GetClientAsync()
    {
        return await _temporalService.GetClientAsync();
    }
    
    public static async Task CleanupAsync()
    {
        await _temporalService.DisconnectAsync();
        _temporalService.Dispose();
    }
}
```

### Application Shutdown

Ensure proper cleanup:

```csharp
// At application startup
var temporalService = ServiceFactory.CreateTemporalClientService(config);

// Register shutdown handler
AppDomain.CurrentDomain.ProcessExit += async (sender, args) =>
{
    await temporalService.DisconnectAsync();
    temporalService.Dispose();
};

// Use the service...
```

## Namespaces

Temporal supports multiple namespaces for isolation:

```csharp
// Development namespace
var devConfig = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "development"
};

// Production namespace
var prodConfig = new TemporalConfiguration
{
    ServerUrl = "temporal.prod.example.com:7233",
    Namespace = "production"
};

// Create separate services for each namespace
var devService = ServiceFactory.CreateTemporalClientService(devConfig);
var prodService = ServiceFactory.CreateTemporalClientService(prodConfig);
```

## Troubleshooting

### Connection Refused

If connection is refused:
1. Verify Temporal server is running
2. Check the server URL and port
3. Ensure network connectivity
4. Verify firewall rules

### TLS Errors

If experiencing TLS errors:
1. Verify certificate and key are correct
2. Ensure certificate is not expired
3. Check that certificate matches the server
4. Verify both cert and key are provided together

### Timeout on Connection

If connection times out:
1. Increase `RetryDelaySeconds`
2. Increase `MaxRetryAttempts`
3. Check network latency
4. Verify server is responsive

### Health Check Failures

```csharp
if (!temporalService.IsConnectionHealthy())
{
    Console.WriteLine("Connection unhealthy, forcing reconnect...");
    await temporalService.ForceReconnectAsync();
    
    var client = await temporalService.GetClientAsync();
    Console.WriteLine("Reconnected successfully");
}
```

## Examples

See the [Examples](Examples/TemporalClientExample.cs) directory for complete working examples.



