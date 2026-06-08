# XiansAi.Lib

A comprehensive .NET library for building enterprise-grade AI agent systems on the Xians platform. It provides workflow orchestration via [Temporal](https://temporal.io/), agent-to-agent (A2A) communication, knowledge management, multi-tenancy, scheduling, secrets, and more — all with built-in resilience and security.

## Packages

| Package | Description |
|---------|-------------|
| [`Xians.Lib`](Xians.Lib/) | Core library — agents, workflows, messaging, knowledge, A2A, scheduling, secrets, tasks, HTTP/Temporal clients |

## Solution Structure

```
XiansAi.Lib/
├── Xians.Lib/               # Core library (NuGet: Xians.Lib)
│   ├── Agents/
│   │   ├── Core/            # XiansPlatform, XiansAgent, XiansWorkflow, XiansContext
│   │   ├── A2A/             # Agent-to-Agent communication
│   │   ├── Documents/       # Document handling
│   │   ├── Knowledge/       # Knowledge upload and retrieval
│   │   ├── Messaging/       # User chat messages and webhooks
│   │   ├── Metrics/         # Usage tracking
│   │   ├── Scheduling/      # Scheduled workflows and cron jobs
│   │   ├── Secrets/         # Secret vault integration
│   │   ├── Tasks/           # Human-in-the-loop (HITL) tasks
│   │   └── Workflows/       # Workflow definitions
│   ├── Common/              # Shared infrastructure, security, caching, multi-tenancy
│   ├── Configuration/       # ServerConfiguration, TemporalConfiguration
│   ├── Http/                # Resilient HTTP client service
│   ├── Logging/             # Structured logging
│   └── Temporal/            # Temporal client service
├── Xians.Lib.Tests/         # Unit and integration tests
└── Xians.Examples/          # Working example projects
    ├── SimpleAgent/
    ├── LeadDiscoveryAgent/
    ├── CustomWorkflow/
    ├── A2ACustomWorkflow/
    ├── KnowledgeAccess/
    ├── ScheduledWorkflow/
    ├── ProgressIndicators/
    ├── WebhookTest/
    └── FileUpload/
```

## Installation

```bash
dotnet add package Xians.Lib
```

## Quick Start

### 1. Initialize the Platform

```csharp
using Xians.Lib.Agents.Core;

var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")!,
    ApiKey    = Environment.GetEnvironmentVariable("XIANS_API_KEY")!,
});
```

### 2. Register an Agent

```csharp
var agent = xiansPlatform.Agents.Register(new()
{
    Name = "My Agent",
    SamplePrompts = ["Say hello", "Tell me the time"],
});
```

### 3. Define a Conversational Workflow

```csharp
var workflow = agent.Workflows.DefineBuiltIn(name: "Conversing Workflow");

workflow.OnUserChatMessage(async (context) =>
{
    await context.ReplyAsync("Hello! How can I help you?");
});
```

### 4. Handle Webhooks

```csharp
var webhookWorkflow = agent.Workflows.DefineBuiltIn(name: "Webhook Workflow");

webhookWorkflow.OnWebhook((context) =>
{
    Console.WriteLine($"Received: {context.Webhook.Name}");
    context.Respond(new { status = "success" });
});
```

### 5. Run the Agent

```csharp
await agent.RunAllAsync();
```

## Key Features

### Agent & Workflow Orchestration
- Register agents with named workflows backed by [Temporal](https://temporal.io/)
- Built-in conversational and webhook workflow types
- Custom workflow definitions with full Temporal power
- Scheduled / cron workflows

### Agent-to-Agent (A2A) Communication
- Send and receive messages between agents
- Contextual A2A requests and structured responses

### Knowledge Management
- Upload markdown, text, and JSON knowledge resources (embedded or file-based)
- Retrieve knowledge by name inside workflow handlers

### Human-in-the-Loop (HITL) Tasks
- Pause a workflow and await human approval or input
- Resume automatically on task completion

### Scheduling
- Cron and interval-based workflow scheduling
- Manage and delete Temporal schedules programmatically

### Secret Vault
- Securely store and retrieve per-tenant secrets at runtime

### Resilient HTTP Client
- Automatic retry with exponential backoff
- Health monitoring and auto-reconnection
- TLS 1.2/1.3 enforcement and certificate-based authentication

### Multi-Tenancy
- Tenant and user identity derived from X.509 certificates
- Isolated namespaces per tenant in Temporal

### Logging
- Configurable console and server log levels
- Structured log shipping to the Xians platform

## Authentication

The `ApiKey` must be a **Base64-encoded X.509 certificate** containing:
- `O=` (Organization) — your tenant ID
- `CN=` (Common Name) — your user ID

```bash
# Correct — Base64-encoded certificate
XIANS_API_KEY=MIIDXTCCAkWgAwIBAgIJAKL5g3aN3dqKMA0GCSqGSIb3DQEBCwUA...
```

See [Authentication Guide](Xians.Lib/docs/Authentication.md) for certificate generation details.

## Environment Variables

| Variable | Description |
|----------|-------------|
| `XIANS_SERVER_URL` | Xians platform server URL |
| `XIANS_API_KEY` | Base64-encoded X.509 certificate |
| `OPENAI_API_KEY` | OpenAI API key (for MAF-based agents) |

## Documentation

Full guides are in [`Xians.Lib/docs/`](Xians.Lib/docs/):

| Guide | Description |
|-------|-------------|
| [Getting Started](Xians.Lib/docs/GettingStarted.md) | Installation and first agent |
| [Configuration](Xians.Lib/docs/Configuration.md) | All configuration options |
| [HTTP Client](Xians.Lib/docs/HttpClient.md) | Resilient HTTP client usage |
| [Messaging](Xians.Lib/docs/Messaging.md) | Chat and webhook messaging |
| [Knowledge](Xians.Lib/docs/Knowledge.md) | Knowledge upload and retrieval |
| [Sub-Workflows](Xians.Lib/docs/SubWorkflows.md) | Child workflow composition |
| [Scheduling](Xians.Lib/docs/Scheduling.md) | Cron and interval scheduling |
| [HITL Tasks](Xians.Lib/docs/HITL_Tasks.md) | Human-in-the-loop tasks |
| [Secret Vault](Xians.Lib/docs/SecretVault.md) | Secret management |
| [Multi-Tenancy](Xians.Lib/docs/Multi-tenancy.md) | Multi-tenant architecture |
| [A2A](Xians.Lib/docs/A2A.md) | Agent-to-agent communication |
| [Documents](Xians.Lib/docs/Documents.md) | Document handling |
| [Exception Handling](Xians.Lib/docs/ExceptionHandling.md) | Error handling patterns |
| [Logging Configuration](Xians.Lib/docs/LoggingConfiguration.md) | Logging setup |

## Examples

Browse working examples in [`Xians.Examples/`](Xians.Examples/):

| Example | Description |
|---------|-------------|
| [SimpleAgent](Xians.Examples/SimpleAgent/) | Minimal agent with chat and webhook workflows |
| [LeadDiscoveryAgent](Xians.Examples/LeadDiscoveryAgent/) | MAF-based agent for lead discovery |
| [CustomWorkflow](Xians.Examples/CustomWorkflow/) | Custom Temporal workflow definition |
| [A2ACustomWorkflow](Xians.Examples/A2ACustomWorkflow/) | Agent-to-agent workflow |
| [KnowledgeAccess](Xians.Examples/KnowledgeAccess/) | Knowledge upload and retrieval |
| [ScheduledWorkflow](Xians.Examples/ScheduledWorkflow/) | Cron-scheduled workflow |
| [ProgressIndicators](Xians.Examples/ProgressIndicators/) | Streaming progress updates |
| [WebhookTest](Xians.Examples/WebhookTest/) | Webhook handling |
| [FileUpload](Xians.Examples/FileUpload/) | Document / file upload |

## Releases

To publish a new version, create and push a version tag:

```bash
export VERSION=1.0.0   # or 1.0.0-beta for pre-release

git tag -a v$VERSION -m "Release v$VERSION"
git push origin v$VERSION
```

## Requirements

- .NET 10.0+
- A running [Temporal](https://temporal.io/) server (managed by the Xians platform)
- Xians platform server URL and API key

## License

Copyright (c) 99x. All rights reserved.
