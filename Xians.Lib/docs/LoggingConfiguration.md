# Logging Configuration Guide

## Overview

The Xians platform now supports **programmatic logging configuration** through `XiansOptions`, giving you full control over log levels without relying on environment variables.

## Quick Start

```csharp
using Xians.Lib.Agents.Core;
using Microsoft.Extensions.Logging;

var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    
    // Configure logging programmatically
    ConsoleLogLevel = LogLevel.Information,  // Console output threshold
    ServerLogLevel = LogLevel.Warning         // Server upload threshold
});
```

## Configuration Options

### 1. Programmatic Configuration (Recommended)

Set log levels directly in `XiansOptions`:

```csharp
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ConsoleLogLevel = LogLevel.Debug,    // Show Debug and above in console
    ServerLogLevel = LogLevel.Error      // Upload Error and above to server
});
```

### 2. Environment Variables (Legacy)

```bash
# In your .env file
CONSOLE_LOG_LEVEL=DEBUG
API_LOG_LEVEL=ERROR
```

### 3. Hybrid Approach

You can use both methods. **Programmatic configuration takes precedence**:

```csharp
// .env file has: CONSOLE_LOG_LEVEL=DEBUG

var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ConsoleLogLevel = LogLevel.Information  // This overrides the .env setting
    // ServerLogLevel not set, will fall back to SERVER_LOG_LEVEL env var or Error default
});
```

## Log Level Reference

| Level | Value | Description | Typical Use |
|-------|-------|-------------|-------------|
| `LogLevel.Trace` | 0 | Most verbose | Detailed diagnostics |
| `LogLevel.Debug` | 1 | Debug info | Development, troubleshooting |
| `LogLevel.Information` | 2 | General info | Production console output |
| `LogLevel.Warning` | 3 | Warnings | Potential issues |
| `LogLevel.Error` | 4 | Errors | Failures (default for API) |
| `LogLevel.Critical` | 5 | Critical | Fatal errors |

## Default Values

If not specified:

- **ConsoleLogLevel**: `LogLevel.Debug` (from env var `CONSOLE_LOG_LEVEL` or default)
- **ServerLogLevel**: `LogLevel.Error` (from env var `SERVER_LOG_LEVEL` or legacy `API_LOG_LEVEL` or default)

## What Gets Logged Where?

### Console Logging

Logs at or **above** `ConsoleLogLevel` are displayed in the console.

### Server Logging (Upload)

Logs at or **above** `ServerLogLevel` are uploaded to the Xians server.

> **📤 Batch Upload:** Logs are uploaded in batches every **60 seconds** (up to **100 logs per batch**). This means logs may take up to 1 minute to appear on the server. On application shutdown, all pending logs are automatically flushed.

> **⏰ Retention:** Server logs are retained for **15 days by default** (MongoDB TTL). After 15 days, logs are automatically deleted. To change retention, contact your server admin or modify `mongodb-indexes.yaml`.

### Example Behavior

With `ConsoleLogLevel = Information` and `ServerLogLevel = Error`:

```csharp
var logger = Logger<MyClass>.For();

logger.LogTrace("Trace");              // ❌ Console  ❌ Server
logger.LogDebug("Debug");              // ❌ Console  ❌ Server
logger.LogDebug("Info");         // ✅ Console  ❌ Server
logger.LogWarning("Warning");          // ✅ Console  ❌ Server
logger.LogError("Error");              // ✅ Console  ✅ Server
logger.LogCritical("Critical");        // ✅ Console  ✅ Server
```

## Common Scenarios

### Development Environment

Maximum visibility for debugging:

```csharp
ConsoleLogLevel = LogLevel.Debug,     // See everything in console
ServerLogLevel = LogLevel.Information  // Upload Info+ to server for analysis
```

### Production Environment

Minimize noise, capture important issues:

```csharp
ConsoleLogLevel = LogLevel.Information,  // General operational info
ServerLogLevel = LogLevel.Error          // Only upload errors to server
```

### Troubleshooting Issues

Temporarily increase logging:

```csharp
ConsoleLogLevel = LogLevel.Trace,     // Maximum detail in console
ServerLogLevel = LogLevel.Warning      // Capture warnings to server
```

### Cost Optimization

Reduce server API calls and storage:

```csharp
ConsoleLogLevel = LogLevel.Information,  // Normal console output
ServerLogLevel = LogLevel.Critical       // Only upload critical failures
```

## Implementation Details

### How It Works

1. `XiansPlatform.InitializeAsync()` receives your `XiansOptions`
2. Calls `LoggerFactory.ConfigureLogLevels()` with your settings
3. All subsequent loggers use these configured levels
4. Falls back to environment variables if options are not provided

### Thread Safety

The logging configuration is thread-safe and applies globally after initialization.

### Dynamic Reconfiguration

Currently, log levels are set at initialization. To change them at runtime:

```csharp
// Not currently supported - requires platform restart
// Future enhancement could add dynamic reconfiguration
```

## Migration Guide

### From Environment Variables Only

**Before:**
```bash
# .env file
CONSOLE_LOG_LEVEL=INFO
SERVER_LOG_LEVEL=ERROR  # or API_LOG_LEVEL=ERROR (legacy)
```

```csharp
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey
});
```

**After:**
```csharp
using Microsoft.Extensions.Logging;

var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ConsoleLogLevel = LogLevel.Information,
    ServerLogLevel = LogLevel.Error
});
```

## How Logs Are Uploaded

### Batch Upload Mechanism

Logs are uploaded to the server in **periodic batches**, not immediately:

- **Batch Size:** 100 logs per batch (default)
- **Upload Interval:** Every 60 seconds (default)
- **Delay:** Logs may take up to 60 seconds to appear on server
- **On Shutdown:** All pending logs are flushed automatically
- **Retry:** Failed uploads are requeued and retried

### Customize Batch Settings (Optional)

```csharp
using Xians.Lib.Logging;

// Customize batch upload settings
LoggingServices.ConfigureBatchSettings(
    batchSize: 50,              // Smaller batches
    processingIntervalMs: 30000 // Upload every 30 seconds
);
```

**When to customize:**
- Smaller batches + frequent uploads → Critical systems needing near real-time logs
- Larger batches + less frequent → High-volume systems to reduce API calls

## Best Practices

1. ✅ **Use programmatic configuration** for explicit, self-documenting code
2. ✅ **Set `ConsoleLogLevel` lower than `ServerLogLevel`** to reduce server load
3. ✅ **Start with defaults**, adjust based on needs
4. ✅ **Log at appropriate levels** in your code (don't over-log at Error)
5. ❌ **Don't set both to Trace** in production (performance impact)

## Troubleshooting

### Logs not showing in console

**Check:**
1. Is `ConsoleLogLevel` set too high?
2. Are you logging at the right level?

```csharp
// Set to Debug to see more
ConsoleLogLevel = LogLevel.Debug
```

### Logs not appearing on server

**Check:**
1. Is `ServerLogLevel` set too high?
2. Verify logs are at or above the threshold
3. Have you waited at least 60 seconds? (logs upload in batches)
4. Is `XiansPlatform.InitializeAsync()` called? (auto-initializes logging)

```csharp
// Lower threshold to upload more
ServerLogLevel = LogLevel.Information
```

**Note:** `XiansPlatform.InitializeAsync()` automatically calls `LoggingServices.Initialize()` with the HTTP client service. You don't need to initialize logging manually.

### Too many logs on server

**Solution:**
```csharp
// Raise threshold to upload less
ServerLogLevel = LogLevel.Critical
```

## See Also

- [Logger Wrapper Guide](../Logging/LOGGER_WRAPPER_GUIDE.md) - How to use `Logger<T>`
- [Getting Started](GettingStarted.md) - Platform initialization
- [Configuration](Configuration.md) - Other configuration options
