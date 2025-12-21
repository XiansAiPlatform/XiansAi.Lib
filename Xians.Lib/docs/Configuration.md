# Configuration Guide

Complete reference for configuring Xians.Lib services.

## ServerConfiguration

Configuration for HTTP client services.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServerUrl` | `string` | **required** | Base URL of the server (e.g., `https://api.example.com`) |
| `ApiKey` | `string` | **required** | API key or certificate in Base64 format |
| `MaxRetryAttempts` | `int` | `3` | Maximum number of retry attempts for failed requests |
| `RetryDelaySeconds` | `int` | `2` | Base delay between retries (uses exponential backoff) |
| `TimeoutSeconds` | `int` | `300` | HTTP request timeout in seconds (5 minutes) |
| `MaxConnectionsPerServer` | `int` | `10` | Maximum number of concurrent connections per server |
| `PooledConnectionLifetimeMinutes` | `int` | `15` | How long connections can live before being recycled |
| `PooledConnectionIdleTimeoutMinutes` | `int` | `2` | How long idle connections are kept in the pool |
| `HealthCheckIntervalMinutes` | `int` | `1` | How often to perform health checks |

### Example

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key",
    
    // Customize retry behavior
    MaxRetryAttempts = 5,
    RetryDelaySeconds = 3,
    
    // Customize timeouts
    TimeoutSeconds = 120,
    
    // Customize connection pooling
    MaxConnectionsPerServer = 20,
    PooledConnectionLifetimeMinutes = 30
};
```

### Environment Variables

When using `ServiceFactory.CreateHttpClientServiceFromEnvironment()`:

| Variable | Required | Description |
|----------|----------|-------------|
| `SERVER_URL` | Yes | Server base URL |
| `API_KEY` | Yes | API key or certificate |

### Validation

The configuration is automatically validated when creating a service. Invalid configurations will throw `InvalidOperationException` with a descriptive message.

## TemporalConfiguration

Configuration for Temporal client services.

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServerUrl` | `string` | **required** | Temporal server URL (e.g., `localhost:7233`) |
| `Namespace` | `string` | **required** | Temporal namespace |
| `CertificateBase64` | `string?` | `null` | Optional TLS certificate in Base64 format (for mTLS) |
| `PrivateKeyBase64` | `string?` | `null` | Optional TLS private key in Base64 format (for mTLS) |
| `MaxRetryAttempts` | `int` | `3` | Maximum number of connection retry attempts |
| `RetryDelaySeconds` | `int` | `5` | Delay between retry attempts in seconds |
| `IsTlsEnabled` | `bool` | computed | Automatically `true` if both certificate and key are provided |

### Example

```csharp
// Without TLS
var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default"
};

// With mTLS
var config = new TemporalConfiguration
{
    ServerUrl = "temporal.example.com:7233",
    Namespace = "production",
    CertificateBase64 = "cert-base64-string",
    PrivateKeyBase64 = "key-base64-string",
    MaxRetryAttempts = 5
};
```

### Environment Variables

When using `ServiceFactory.CreateTemporalClientServiceFromEnvironment()`:

| Variable | Required | Description |
|----------|----------|-------------|
| `TEMPORAL_SERVER_URL` | Yes | Temporal server URL |
| `TEMPORAL_NAMESPACE` | Yes | Temporal namespace |
| `TEMPORAL_CERT_BASE64` | No | TLS certificate (Base64) |
| `TEMPORAL_KEY_BASE64` | No | TLS private key (Base64) |

**Note:** For mTLS, both `TEMPORAL_CERT_BASE64` and `TEMPORAL_KEY_BASE64` must be provided together.

### Validation

TLS configuration is validated to ensure:
- Both certificate and private key are provided together (or both omitted)
- Server URL and namespace are not empty
- Retry settings are valid (non-negative)

## Custom Logger Configuration

You can provide a custom logger factory for the entire library:

```csharp
using Xians.Lib.Common;
using Microsoft.Extensions.Logging;

// Set a custom logger factory
LoggerFactory.Instance = myCustomLoggerFactory;

// Or create with custom minimum level
LoggerFactory.Instance = LoggerFactory.CreateDefaultLoggerFactory(LogLevel.Debug);
```

Or provide a logger per service:

```csharp
var logger = myLoggerFactory.CreateLogger<HttpClientService>();
var httpService = ServiceFactory.CreateHttpClientService(config, logger);
```

## Best Practices

### Production Settings

For production environments, consider:

```csharp
var config = new ServerConfiguration
{
    ServerUrl = Environment.GetEnvironmentVariable("SERVER_URL")!,
    ApiKey = Environment.GetEnvironmentVariable("API_KEY")!,
    
    // Longer timeout for production workloads
    TimeoutSeconds = 600,
    
    // More retry attempts for resilience
    MaxRetryAttempts = 5,
    RetryDelaySeconds = 3,
    
    // Larger connection pool for high throughput
    MaxConnectionsPerServer = 50,
    
    // Longer connection lifetime
    PooledConnectionLifetimeMinutes = 30
};
```

### Development Settings

For local development:

```csharp
var config = new ServerConfiguration
{
    ServerUrl = "http://localhost:5000",
    ApiKey = "dev-api-key",
    
    // Shorter timeout for faster feedback
    TimeoutSeconds = 30,
    
    // Fewer retries for quicker failure
    MaxRetryAttempts = 2,
    
    // Smaller connection pool
    MaxConnectionsPerServer = 5
};
```

### Security Considerations

1. **Never hardcode credentials** - Always use environment variables or secure configuration stores
2. **Use HTTPS in production** - Ensure `ServerUrl` uses HTTPS scheme
3. **Certificate validation** - The library enforces TLS 1.2/1.3 and validates server certificates
4. **Rotate credentials regularly** - Update API keys and certificates periodically

