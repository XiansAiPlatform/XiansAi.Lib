# Xians.Lib Documentation

Welcome to the Xians.Lib documentation! This library provides robust HTTP and Temporal server connection management with built-in resilience.

## 📚 Documentation

### Getting Started
- **[Getting Started Guide](GettingStarted.md)** - Quick start guide and basic examples

### Configuration
- **[Configuration Guide](Configuration.md)** - Complete configuration reference for HTTP and Temporal services

### Guides
- **[HTTP Client Guide](HttpClient.md)** - Comprehensive HTTP client usage and best practices
- **[Temporal Client Guide](TemporalClient.md)** - Comprehensive Temporal client usage and best practices
- **[Knowledge Guide](Knowledge.md)** - Store and manage agent knowledge (prompts, configs, docs)
- **[Caching Guide](Caching.md)** - Improve performance with automatic caching
- **[System-Scoped Agents](SystemScopedAgents.md)** - Multi-tenant agent architecture and tenant isolation
- **[Worker Registration](WorkerRegistration.md)** - Temporal worker setup and management

### Code Examples
- **[HTTP Client Examples](Examples/HttpClientExample.cs)** - Working code examples for HTTP operations
- **[Temporal Client Examples](Examples/TemporalClientExample.cs)** - Working code examples for Temporal operations

## 🎯 Quick Links

### Common Tasks

**Creating an HTTP Client:**
```csharp
var config = new ServerConfiguration
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key"
};
using var httpService = ServiceFactory.CreateHttpClientService(config);
```

**Creating a Temporal Client:**
```csharp
var config = new TemporalConfiguration
{
    ServerUrl = "localhost:7233",
    Namespace = "default"
};
using var temporalService = ServiceFactory.CreateTemporalClientService(config);
var client = await temporalService.GetClientAsync();
```

## 📖 Table of Contents

1. [Getting Started](GettingStarted.md)
   - Installation
   - Quick Start (HTTP)
   - Quick Start (Temporal)

2. [Configuration](Configuration.md)
   - ServerConfiguration Reference
   - TemporalConfiguration Reference
   - Environment Variables
   - Validation
   - Best Practices

3. [HTTP Client](HttpClient.md)
   - Basic Usage
   - Advanced Features
   - Authentication
   - Error Handling
   - Performance Optimization
   - Thread Safety
   - Best Practices
   - Troubleshooting

4. [Temporal Client](TemporalClient.md)
   - Basic Usage
   - Secure Connections (mTLS)
   - Advanced Features
   - Working with Workflows
   - Error Handling
   - Configuration Best Practices
   - Thread Safety
   - Lifecycle Management
   - Troubleshooting

5. [Knowledge](Knowledge.md)
   - What is Knowledge
   - Storing and Retrieving Knowledge
   - Knowledge Types
   - Scoping and Isolation
   - Real-World Examples
   - Best Practices

6. [Caching](Caching.md)
   - How Caching Works
   - Configuration Options
   - Performance Impact
   - Auto-Invalidation
   - Troubleshooting

7. [System-Scoped Agents](SystemScopedAgents.md)
   - Multi-Tenant Architecture
   - Tenant Isolation
   - Usage Examples
   - Security Best Practices
   - Troubleshooting

8. [Worker Registration](WorkerRegistration.md)
   - Temporal Worker Setup
   - Task Queue Configuration
   - Worker Lifecycle

9. [Examples](Examples/)
   - HTTP Client Examples
   - Temporal Client Examples

## 🔧 Support

For issues and questions:
- Review the relevant guide above
- Check the [Examples](Examples/) directory
- Consult the inline code documentation

## 📝 License

Copyright (c) 99x. All rights reserved.


