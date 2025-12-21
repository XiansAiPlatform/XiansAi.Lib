# Getting Started with Xians.Lib

This guide will help you get up and running with Xians.Lib quickly.

## Installation

Add Xians.Lib to your project:

```bash
dotnet add package Xians.Lib
```

## Quick Start

### 1. HTTP Client Service

The HTTP client service provides resilient HTTP connections with automatic retry and health monitoring.

#### Basic Usage

```csharp
using Xians.Lib.Configuration;
using Xians.Lib.Http;
using Xians.Lib.Common;

// Configure the service
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key-here"
};

// Create the service
using var httpService = ServiceFactory.CreateHttpClientService(config);

// Make requests with automatic retry
var response = await httpService.GetWithRetryAsync("/api/users");
var content = await response.Content.ReadAsStringAsync();
```

#### Using Environment Variables

```csharp
// Set environment variables
Environment.SetEnvironmentVariable("SERVER_URL", "https://api.example.com");
Environment.SetEnvironmentVariable("API_KEY", "your-api-key");

// Create from environment
using var httpService = ServiceFactory.CreateHttpClientServiceFromEnvironment();

// Use the service
var response = await httpService.GetWithRetryAsync("/api/data");
```

### 2. Temporal Client Service

The Temporal client service manages connections to Temporal workflow servers.

#### Basic Usage

```csharp
using Xians.Lib.Configuration;
using Xians.Lib.Temporal;
using Xians.Lib.Common;

// Configure the service
var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default"
};

// Create the service
using var temporalService = ServiceFactory.CreateTemporalClientService(config);

// Get the client (connects automatically)
var client = await temporalService.GetClientAsync();

// Use the client for workflow operations
var workflowHandle = await client.StartWorkflowAsync(...);
```

#### With mTLS Support

```csharp
var config = new TemporalConfiguration
{
    ServerUrl = "temporal.example.com:7233",
    Namespace = "production",
    CertificateBase64 = "your-cert-base64",
    PrivateKeyBase64 = "your-key-base64"
};

using var temporalService = ServiceFactory.CreateTemporalClientService(config);
var client = await temporalService.GetClientAsync();
```

## Next Steps

- [Configuration Guide](Configuration.md) - Learn about all configuration options
- [HTTP Client Guide](HttpClient.md) - Advanced HTTP client usage
- [Temporal Client Guide](TemporalClient.md) - Advanced Temporal usage
- [Examples](Examples/) - Browse code examples


