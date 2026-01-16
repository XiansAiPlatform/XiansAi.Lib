# Xians.Lib

A robust .NET library for establishing HTTP and Temporal server connections with built-in resilience, retry logic, and health monitoring.

## Releases

```bash
# Define the version
export VERSION=1.3.7 # or 1.3.7-beta for pre-release

# Create and push a version tag
git tag -a v$VERSION -m "Release v$VERSION"
git push origin v$VERSION
```

## Installation

```bash
dotnet add package Xians.Lib
```

## ⚠️ Important: API Key Format

**The API key must be a Base64-encoded X.509 certificate**, not a simple string.

The certificate must contain:
- **Organization (O=)** field with your tenant ID
- **Common Name (CN=)** field with your user ID

Example:
```bash
# ❌ WRONG - Simple string will fail
API_KEY=my-api-key-123

# ✅ CORRECT - Base64-encoded certificate
API_KEY=MIIDXTCCAkWgAwIBAgIJAKL5g3aN3dqKMA0GCSqGSIb3DQEBCwUA...
```

See [Authentication Guide](docs/Authentication.md) for details.

## Quick Start

**Recommended: Initialize from Environment (Fetches Temporal config from server):**
```csharp
using Xians.Lib.Common;

// Set environment variables:
// SERVER_URL=https://api.example.com
// API_KEY=your-api-key

var (httpService, temporalService) = await ServiceFactory.CreateServicesFromEnvironmentAsync();

// Use HTTP service
var response = await httpService.GetWithRetryAsync("/api/data");

// Use Temporal service (config fetched from server)
var temporalClient = await temporalService.GetClientAsync();
```

**Alternative: Direct initialization:**
```csharp
using Xians.Lib.Common;

var (httpService, temporalService) = await ServiceFactory.CreateServicesFromServerAsync(
    serverUrl: "https://api.example.com",
    apiKey: "your-api-key"
);
// Temporal settings automatically fetched from: GET /api/agent/settings/flowserver
```

**Manual Configuration (Advanced):**
```csharp
using Xians.Lib.Configuration;
using Xians.Lib.Common;

// HTTP only
var httpConfig = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key"
};
using var httpService = ServiceFactory.CreateHttpClientService(httpConfig);

// Temporal only (manual config)
var temporalConfig = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default"
};
using var temporalService = ServiceFactory.CreateTemporalClientService(temporalConfig);
```

## 📚 Documentation

**Comprehensive guides and examples are available in the [docs](docs/) directory:**

- **[Getting Started](docs/GettingStarted.md)** - Installation and quick start
- **[Configuration](docs/Configuration.md)** - Complete configuration reference
- **[HTTP Client Guide](docs/HttpClient.md)** - HTTP client usage and best practices
- **[Sub-Workflows](docs/SubWorkflows.md)** - Child workflow orchestration and composition
- **[Scheduling](docs/Scheduling.md)** - Workflow scheduling and cron jobs
- **[Multi-Tenancy](docs/Multi-tenancy.md)** - Multi-tenant architecture and isolation
- **[Examples](docs/Examples/)** - Working code examples

## 🔑 Key Features

**Resilience:**
- Automatic retry with exponential backoff
- Health monitoring and auto-reconnection
- Connection pooling and lifecycle management

**Security:**
- TLS 1.2/1.3 enforcement
- Certificate-based authentication
- mTLS support for Temporal

**Developer-Friendly:**
- Simple configuration with validation
- Extension methods for common operations
- Thread-safe for concurrent use
- Comprehensive logging

## 📝 License

Copyright (c) 99x. All rights reserved.

