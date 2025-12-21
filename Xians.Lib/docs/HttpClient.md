# HTTP Client Guide

Comprehensive guide for using the HTTP client service in Xians.Lib.

## Overview

The HTTP client service provides:
- **Resilient connections** with automatic retry and reconnection
- **Health monitoring** to detect and recover from connection issues
- **Connection pooling** for optimal performance
- **Security** with TLS 1.2/1.3 enforcement and certificate support
- **Extension methods** for simplified HTTP operations

## Basic Usage

### Creating the Service

```csharp
using Xians.Lib.Configuration;
using Xians.Lib.Http;
using Xians.Lib.Common;

var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key"
};

using var httpService = ServiceFactory.CreateHttpClientService(config);
```

### Making Requests

#### Using Extension Methods (Recommended)

```csharp
// GET request
var response = await httpService.GetWithRetryAsync("/api/users");
var users = await response.Content.ReadAsStringAsync();

// POST request
var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
var response = await httpService.PostWithRetryAsync("/api/users", content);

// PUT request
var response = await httpService.PutWithRetryAsync("/api/users/123", content);

// DELETE request
var response = await httpService.DeleteWithRetryAsync("/api/users/123");

// PATCH request
var response = await httpService.PatchWithRetryAsync("/api/users/123", content);
```

#### Using the Underlying HttpClient

```csharp
var client = httpService.Client;
var response = await client.GetAsync("/api/data");
```

## Advanced Features

### Custom Retry Logic

Execute any operation with automatic retry:

```csharp
var result = await httpService.ExecuteWithRetryAsync(async () =>
{
    var client = await httpService.GetHealthyClientAsync();
    var response = await client.GetAsync("/api/data");
    response.EnsureSuccessStatusCode();
    
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<MyData>(json);
});
```

### Health Monitoring

Check the service health:

```csharp
// Async health check (makes a lightweight HTTP request)
bool isHealthy = await httpService.IsHealthyAsync();

if (!isHealthy)
{
    Console.WriteLine("Service is unhealthy, will auto-reconnect");
}
```

### Manual Reconnection

Force a reconnection:

```csharp
await httpService.ForceReconnectAsync();
Console.WriteLine("Reconnected to server");
```

### Getting a Healthy Client

Ensure you have a healthy client before critical operations:

```csharp
var client = await httpService.GetHealthyClientAsync();
// Client is guaranteed to be connected and healthy
var response = await client.GetAsync("/api/critical-operation");
```

### Connection Testing

Test the connection explicitly:

```csharp
try
{
    await httpService.TestConnectionAsync();
    Console.WriteLine("Connection successful");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
```

## Authentication

### API Key Authentication

The simplest form - provide an API key as a Bearer token:

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key-string"  // Regular API key
};
```

The library will automatically add: `Authorization: Bearer your-api-key-string`

### Certificate-Based Authentication

Provide a certificate in Base64 format:

```csharp
var certBytes = File.ReadAllBytes("client-cert.pfx");
var certBase64 = Convert.ToBase64String(certBytes);

var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = certBase64  // Certificate as Base64
};
```

The library will:
1. Parse the certificate
2. Validate expiration dates
3. Export it and add as: `Authorization: Bearer <cert-base64>`

## Error Handling

### Transient Errors (Auto-Retry)

The library automatically retries these errors:
- Network timeouts
- Connection failures
- Socket exceptions
- DNS errors
- SSL/TLS handshake errors
- HTTP request exceptions

### Non-Transient Errors (Immediate Failure)

These errors fail immediately without retry:
- HTTP 4xx client errors (except timeouts)
- Invalid configurations
- Certificate validation failures
- Non-network exceptions

### Example Error Handling

```csharp
try
{
    var response = await httpService.GetWithRetryAsync("/api/data");
    response.EnsureSuccessStatusCode();
}
catch (HttpRequestException ex)
{
    // All retries exhausted or non-transient error
    Console.WriteLine($"Request failed: {ex.Message}");
}
catch (TaskCanceledException ex)
{
    // Request timed out after all retries
    Console.WriteLine($"Request timed out: {ex.Message}");
}
```

## Performance Optimization

### Connection Pooling

Configure connection pool settings:

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key",
    
    // Increase for high-throughput scenarios
    MaxConnectionsPerServer = 50,
    
    // Keep connections alive longer
    PooledConnectionLifetimeMinutes = 30,
    
    // Keep idle connections for quick reuse
    PooledConnectionIdleTimeoutMinutes = 5
};
```

### Timeout Configuration

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key",
    
    // Increase for long-running operations
    TimeoutSeconds = 600  // 10 minutes
};
```

### Retry Configuration

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key",
    
    // More aggressive retry for critical services
    MaxRetryAttempts = 5,
    RetryDelaySeconds = 2  // Base delay, uses exponential backoff
};
```

## Thread Safety

The HTTP client service is **fully thread-safe** and can be safely used from multiple threads:

```csharp
// Share one instance across multiple threads
var httpService = ServiceFactory.CreateHttpClientService(config);

var tasks = Enumerable.Range(0, 10).Select(async i =>
{
    var response = await httpService.GetWithRetryAsync($"/api/item/{i}");
    return await response.Content.ReadAsStringAsync();
});

var results = await Task.WhenAll(tasks);
```

## Best Practices

### 1. Singleton Pattern

Create one instance per application and reuse it:

```csharp
// ✅ Good
public class MyService
{
    private static readonly IHttpClientService _httpService = 
        ServiceFactory.CreateHttpClientServiceFromEnvironment();
    
    public async Task<string> GetDataAsync()
    {
        return await _httpService.GetWithRetryAsync("/api/data");
    }
}

// ❌ Bad - creates new instance for each call
public async Task<string> GetDataAsync()
{
    using var httpService = ServiceFactory.CreateHttpClientService(config);
    return await httpService.GetWithRetryAsync("/api/data");
}
```

### 2. Always Dispose

Use `using` statements or ensure disposal:

```csharp
// ✅ Good
using var httpService = ServiceFactory.CreateHttpClientService(config);
// Use the service...

// ✅ Also good
var httpService = ServiceFactory.CreateHttpClientService(config);
try
{
    // Use the service...
}
finally
{
    httpService.Dispose();
}
```

### 3. Use Extension Methods

Extension methods provide automatic retry:

```csharp
// ✅ Good - automatic retry
var response = await httpService.GetWithRetryAsync("/api/data");

// ❌ Less resilient - no retry
var response = await httpService.Client.GetAsync("/api/data");
```

### 4. Handle Responses Properly

```csharp
var response = await httpService.GetWithRetryAsync("/api/data");

// Check status
if (response.IsSuccessStatusCode)
{
    var content = await response.Content.ReadAsStringAsync();
}
else
{
    Console.WriteLine($"Request failed: {response.StatusCode}");
}
```

## Troubleshooting

### Connection Issues

If experiencing connection problems:

```csharp
// Check health
var isHealthy = await httpService.IsHealthyAsync();
Console.WriteLine($"Service healthy: {isHealthy}");

// Force reconnection
await httpService.ForceReconnectAsync();

// Test connection
await httpService.TestConnectionAsync();
```

### Certificate Errors

If certificate validation fails:
1. Ensure the certificate is valid and not expired
2. Check that the certificate is in the correct format (PFX/PKCS12)
3. Verify the certificate matches the server you're connecting to

### Timeout Issues

If requests are timing out:
1. Increase `TimeoutSeconds` in configuration
2. Check network connectivity
3. Verify the server is responding
4. Consider if the operation genuinely takes that long

## Examples

See the [Examples](Examples/HttpClientExample.cs) directory for complete working examples.

