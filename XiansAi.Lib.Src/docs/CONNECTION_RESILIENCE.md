# Connection Resilience Implementation

## Overview

XiansAi.Lib implements automatic connection resilience for both Temporal and SecureApi connections, providing transparent reconnection after outages of any duration.

## ✅ Current Implementation

### Temporal Connections (`TemporalClientService`)

**Auto-Reconnection Features:**

- **Health Checking**: Every `GetClientAsync()` call performs lightweight health check
- **Retry Logic**: 3 retry attempts with 5-second delays and exponential backoff  
- **Transparent Recovery**: Automatic reconnection on connection failures
- **Resource Management**: Proper disposal and graceful shutdown handling

```csharp
// Automatic reconnection on every access
var client = await TemporalClientService.Instance.GetClientAsync();

// Force reconnection for testing
await service.ForceReconnectAsync();
```

### SecureApi Connections (`SecureApi`)

**Auto-Reconnection Features:**

- **Health Monitoring**: Cached health checks with automatic client recreation
- **Retry Logic**: 3 retry attempts with 2-second delays and exponential backoff
- **Transient Exception Detection**: Smart classification of retriable vs permanent failures
- **Enhanced HttpClient**: Optimized connection pooling and timeout management

```csharp
// Guaranteed healthy client with auto-reconnection
var client = await SecureApi.Instance.GetHealthyClientAsync();

// Extension methods with built-in retry
var response = await client.GetWithRetryAsync("/api/endpoint");
```

## 🔄 Connection Lifecycle

### Temporal

1. Lazy connection creation on first `GetClientAsync()` call
2. Health check on subsequent calls via lightweight workflow query
3. Automatic reconnection if health check fails
4. Singleton pattern ensures connection reuse

### SecureApi  

1. Client initialization with enhanced HttpClientHandler
2. Health status caching with configurable intervals
3. Automatic client recreation on health check failures
4. Connection pooling with optimized timeouts

## ⚙️ Configuration

### Temporal Settings

```csharp
private readonly TimeSpan _connectionRetryDelay = TimeSpan.FromSeconds(5);
private readonly int _maxRetryAttempts = 3;
```

### SecureApi Settings

```csharp
private readonly int _maxRetryAttempts = 3;
private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);
```

### HttpClient Configuration

```csharp
MaxConnectionsPerServer = 10;
PooledConnectionLifetime = TimeSpan.FromMinutes(15);
PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);
```

## 🚨 Exception Handling

### Retriable Exceptions

- `HttpRequestException` with network-related messages
- `TaskCanceledException` (timeouts)
- `SocketException`
- `TimeoutException`

### Error Classification

- **Transient Failures**: Automatically retried with exponential backoff
- **Permanent Failures**: Immediate failure after retry exhaustion
- **Connection-Specific**: Detailed error messages for monitoring

## 💻 Usage Patterns

### Long-Running Applications

```csharp
public async Task RunContinuouslyAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            // Temporal - auto-reconnects transparently
            var temporalClient = await TemporalClientService.Instance.GetClientAsync();
            
            // SecureApi - guaranteed healthy client
            var apiClient = await SecureApi.Instance.GetHealthyClientAsync();
            
            // Perform operations...
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("retry attempts"))
        {
            // Handle permanent connection failures
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }
}
```

### Command Line Applications

```csharp
// Setup graceful shutdown
CommandLineHelper.SetupGracefulShutdown();

// Run with automatic cleanup
await CommandLineHelper.RunWorkflowAsync(runner);
```

## 📊 Monitoring

### Log Levels

- **Info**: Successful connections and reconnections
- **Warning**: Health check failures and retry attempts  
- **Error**: Connection failures after all retries

### Typical Log Output

```text
[INFO] Temporal client connection established successfully
[WARN] SecureApi health check failed, will attempt reconnection
[INFO] SecureApi client recreated successfully after connection recovery
[ERROR] All 3 connection attempts failed
```

## 🎯 Key Benefits

- **Instant Recovery**: Immediate reconnection when services return online
- **Extended Outages**: Handles outages of any duration (minutes to hours)
- **Resource Efficient**: Optimized connection pooling and health checking
- **Production Ready**: Comprehensive error handling and monitoring 