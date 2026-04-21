# Secret Vault

> **TL;DR**: Secret Vault is a secure key-value store for secrets (API keys, tokens, webhook secrets). Start with `agent.Secrets.TenantScope()` (tenant id auto-resolved), then **narrow** with `.AgentScope()`, `.ParticipantScope()`, `.ActivationScope()` as needed, then perform CRUD. Values are encrypted at rest on the server.

## What Is Secret Vault?

Secret Vault stores sensitive key-value pairs with optional scoping:

- **Key** – Unique name (e.g. `api-key`, `webhook-secret`)
- **Value** – The secret (encrypted at rest with AES-256-GCM on the server)
- **Scope** – Tenant, agent, participant (user), and activation for access control
- **AdditionalData** – Optional flat metadata (string/number/boolean only; not for secrets)

### Scope Semantics

| Scope          | Meaning when set                       | Meaning when null                      |
|----------------|----------------------------------------|----------------------------------------|
| TenantId       | Secret only for that tenant            | Cross-tenant (any tenant)              |
| AgentId        | Secret only for that agent             | Across all agents                      |
| UserId         | Only that participant can access       | Any participant may access             |
| ActivationName | Only that agent activation can access  | Any activation of the agent can access |

**Fetch-by-key** uses **strict** scope matching for all scopes (tenant, agent, participant, activation): the request must send the same scope values as stored. If you omit a scope (e.g. no `activationName`), only secrets with that scope null are returned; if you send a scope value, the document must have that exact value.

## Mental Model: Start Wide, Narrow As Needed

Most secrets are scoped only to the **tenant**. Some are also scoped to a specific **agent**. A few are scoped further down to a **participant** (user) or even a single agent **activation**. The API mirrors this — you start broad and chain narrower setters:

```text
TenantScope()                                   ← tenant only (most common)
   └── .AgentScope()                            ← + this agent
          └── .ParticipantScope()               ← + this participant
                 └── .ActivationScope()         ← + this activation
```

Each narrowing method has **two overloads**:

- **No-arg** – auto-resolves the value from the current `XiansContext` (workflow / activity / message handler).
- **`(string?)`** – set an explicit value (or pass `null` to broaden that dimension back).

## Quick Start

```csharp
using Xians.Lib.Agents.Secrets;
using Xians.Lib.Agents.Secrets.Models;

// Tenant-scoped secret (most common). Tenant id auto-resolved.
var secrets = agent.Secrets.TenantScope();

var created = await secrets.CreateAsync("external-api-key", "sk-xxx");
var fetched = await secrets.FetchByKeyAsync("external-api-key");
Console.WriteLine(fetched?.Value); // "sk-xxx"

var list = await secrets.ListAsync();
await secrets.UpdateAsync(created.Id, value: "sk-new");
await secrets.DeleteAsync(created.Id);

// Narrower scope when a secret belongs to one agent
var perAgent = agent.Secrets.TenantScope().AgentScope();

// Narrower still: per-participant secret (e.g. user OAuth token)
var perUser = agent.Secrets.TenantScope().AgentScope().ParticipantScope();

// One activation only
var perActivation = agent.Secrets.TenantScope().AgentScope().ActivationScope();
```

## Key Features

- **Narrowing builder** – Start with `TenantScope()`, then chain `.AgentScope()` → `.ParticipantScope()` → `.ActivationScope()` to narrow.
- **Context-aware** – Each narrowing method has a no-arg overload that auto-resolves from `XiansContext`.
- **Explicit override** – Each narrowing method also has a `(string?)` overload for explicit values; pass `null` to broaden.
- **Escape hatch** – `ScopeUnbound()` returns a fully unscoped builder for admin / cross-tenant flows.
- **Full CRUD** – Create, fetch by key, list, get by id, update, delete.
- **Strict scope** – Fetch-by-key matches scope exactly for all four dimensions.
- **Encrypted at rest** – Server encrypts values; lib only sends/receives plaintext over TLS.
- **Optional metadata** – `additionalData` for flat key-value (env, service name, etc.); not for sensitive data.

## Implementation Overview

### Components in Xians.Lib

| Component | Location | Role |
|-----------|----------|------|
| **SecretVaultCollection** | `Agents/Secrets/SecretVaultCollection.cs` | Entry point on `agent.Secrets`; exposes `TenantScope()` / `TenantScope(tenantId)` / `ScopeUnbound()` |
| **SecretVaultScopeBuilder** | `Agents/Secrets/SecretVaultScopeBuilder.cs` | Fluent narrowing builder + CRUD (Create, FetchByKey, List, GetById, Update, Delete) |
| **Models** | `Agents/Secrets/Models/SecretVaultModels.cs` | DTOs: create/update requests, get/fetch/list responses |

### Server API

The lib calls the **Agent API** at `api/agent/secrets` (client certificate auth). Same backend as the Admin Secret Vault API; see the server's `SECRET_VAULT.md` for encryption, validation, and database details.

## Usage

### 1. Getting a scope builder

```csharp
// Tenant only (recommended starting point). Tenant id is auto-resolved from
// XiansContext.SafeTenantId, falling back to the agent's certificate tenant.
var byTenant = agent.Secrets.TenantScope();

// Tenant + this agent (auto-resolved)
var byAgent = agent.Secrets.TenantScope().AgentScope();

// Tenant + this agent + this participant (auto-resolved from XiansContext)
var byUser = agent.Secrets.TenantScope().AgentScope().ParticipantScope();

// Tenant + this agent + this participant + this activation
var byActivation = agent.Secrets
    .TenantScope().AgentScope().ParticipantScope().ActivationScope();

// Explicit overrides — pass a value to override, or null to broaden again.
var otherTenant   = agent.Secrets.TenantScope("tenant-2");
var otherAgent    = agent.Secrets.TenantScope().AgentScope("agent-x");
var explicitUser  = agent.Secrets.TenantScope().AgentScope().ParticipantScope("user-1");
var anyParticipant = agent.Secrets.TenantScope().AgentScope().ParticipantScope(null);

// Escape hatch: no scope at all (admin / cross-tenant / tests outside a workflow)
var unbound = agent.Secrets.ScopeUnbound();
```

> Inside a workflow / activity / message handler, the no-arg overloads pick up the live tenant, agent, participant, and activation from `XiansContext`. Passing explicit values that conflict with the live context is rejected client-side; see [SecretVaultValidation.md](./SecretVaultValidation.md).

### 2. Create

Creates a secret. **Key must be unique** in the vault.

```csharp
var scoped = agent.Secrets.TenantScope().AgentScope();

// Minimal
var created = await scoped.CreateAsync("my-key", "my-secret-value");

// With optional metadata (flat key-value; values: string, number, or boolean only)
var withMeta = await scoped.CreateAsync(
    "webhook-secret",
    "whsec_xxx",
    new { env = "prod", service = "payment" });
```

Returns `SecretVaultGetResponse` (Id, Key, Value, scope, audit fields).

### 3. Fetch by key

Returns **only** decrypted value and optional additionalData. Use the **same scope** as when the secret was created (strict match).

```csharp
var scoped = agent.Secrets.TenantScope().AgentScope();
var result = await scoped.FetchByKeyAsync("api-key");

if (result != null)
{
    Console.WriteLine(result.Value);
    // result.AdditionalData is object (e.g. JsonElement or dict)
}
else
{
    // Not found or scope mismatch
}
```

### 4. List

Lists secrets filtered by the current scope (tenant / agent / activation). Does **not** return the secret value.

```csharp
var scoped = agent.Secrets.TenantScope();
var items = await scoped.ListAsync();

foreach (var item in items)
{
    Console.WriteLine($"{item.Key} ({item.Id})");
    // item has: Id, Key, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy
}
```

### 5. Get by id

Returns full record including decrypted value. Use when you already have the secret id (e.g. from create or list).

```csharp
var scoped = agent.Secrets.TenantScope();
var secret = await scoped.GetByIdAsync("507f1f77bcf86cd799439011");

if (secret != null)
{
    Console.WriteLine($"{secret.Key}: {secret.Value}");
}
```

### 6. Update

Updates an existing secret by id. Omitted parameters leave existing values unchanged.

```csharp
var scoped = agent.Secrets.TenantScope();

// Update value only
await scoped.UpdateAsync(id, value: "new-secret-value");

// Update value and metadata
await scoped.UpdateAsync(id, value: "new-value", additionalData: new { rev = 2 });

// Update scope (pass explicit tenant/agent/user/activationName to change scope)
await scoped.UpdateAsync(id, tenantId: "t2", agentId: "a2", userId: "u2", activationName: "activation-2");
```

### 7. Delete

Deletes a secret by id. Returns `true` if deleted, `false` if not found.

```csharp
var scoped = agent.Secrets.TenantScope();
bool deleted = await scoped.DeleteAsync(id);
```

## Common Patterns

### Per-tenant API key (shared across agents and participants)

```csharp
// Tenant-only is the default; nothing extra to chain.
var tenantScoped = agent.Secrets.TenantScope();
await tenantScoped.CreateAsync("external-api-key", apiKeyFromConfig);

// Later, in any request for the same tenant:
var key = await agent.Secrets.TenantScope().FetchByKeyAsync("external-api-key");
```

### Per-agent secret

```csharp
var scoped = agent.Secrets.TenantScope().AgentScope();
await scoped.CreateAsync("agent-config-token", token);
var fetched = await scoped.FetchByKeyAsync("agent-config-token");
```

### Per-participant (user) secret

```csharp
// e.g. an OAuth token issued for the current participant
var scoped = agent.Secrets.TenantScope().AgentScope().ParticipantScope();
await scoped.CreateAsync("user-token", token);
var fetched = await scoped.FetchByKeyAsync("user-token");
```

### Per-activation secret (one activation of an agent only)

```csharp
// Inside a workflow, ActivationScope() picks up the current activation from
// XiansContext.SafeIdPostfix.
var scoped = agent.Secrets.TenantScope().AgentScope().ActivationScope();
await scoped.CreateAsync("activation-api-key", key);
var fetched = await scoped.FetchByKeyAsync("activation-api-key");
```

### List then get value for one

```csharp
var scoped = agent.Secrets.TenantScope();
var list = await scoped.ListAsync();
var first = list.FirstOrDefault();
if (first != null)
{
    var full = await scoped.GetByIdAsync(first.Id);
    Console.WriteLine(full?.Value);
}
```

## Data Models

### Request/response types (lib)

- **SecretVaultCreateRequest** – Key, Value, TenantId?, AgentId?, UserId?, ActivationName?, AdditionalData?
- **SecretVaultUpdateRequest** – Value?, TenantId?, AgentId?, UserId?, ActivationName?, AdditionalData?
- **SecretVaultGetResponse** – Id, Key, Value, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
- **SecretVaultFetchResponse** – Value, AdditionalData (fetch-by-key only)
- **SecretVaultListItem** – Id, Key, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy (no Value)

> Note on naming: `UserId` on the wire / model corresponds to **participant** in the SDK builder (`ParticipantScope`). The legacy `UserScope(string?)` setter is still available as an alias.

### AdditionalData rules (server)

- Flat object only; values must be **string**, **number**, or **boolean**
- Keys: `a-zA-Z0-9_.-`, max 128 chars; max 50 keys; total size ≤ 8 KB
- Not for sensitive data; stored as sanitized JSON

## Requirements

- **HTTP service** – Agent must be configured with `IHttpClientService` (e.g. via `XiansPlatform` with server URL and client certificate). Otherwise, Secret Vault methods throw `InvalidOperationException`.
- **Agent API auth** – Server expects client certificate auth for `api/agent/secrets`; the lib uses the same HTTP client as Knowledge/Documents.

## Best Practices

- **Do** start with `TenantScope()` and only narrow when a secret truly belongs to a specific agent / participant / activation.
- **Do** use clear, unique keys (e.g. `external-api-key`, `webhook-{service}`).
- **Do** use the same scope when creating and fetching (strict match).
- **Do** use `additionalData` only for non-sensitive metadata (env, service name).
- **Do** handle `null` from `FetchByKeyAsync` and `GetByIdAsync` (not found or access denied).
- **Don't** store highly sensitive material in `additionalData` (it is not encrypted like the value).
- **Don't** use `ScopeUnbound()` from regular workflow code; reserve it for admin / cross-tenant flows.

## Troubleshooting

### "HTTP service is not configured"

Agent was not initialized with an HTTP client (e.g. not via `XiansPlatform` with ServerUrl/certificate). Use the same setup as for Knowledge/Documents.

### "Cannot resolve tenant id from XiansContext or agent options"

`TenantScope()` (no-arg) needs either an active workflow/activity context or a certificate-tenant configured on the agent. Pass an explicit `TenantScope(tenantId)`, or use `ScopeUnbound()` if you really need cross-tenant.

### "No participant id is available in the current XiansContext"

`ParticipantScope()` (no-arg) was called outside a participant-bearing context. Either pass an explicit `ParticipantScope(participantId)` or omit the call so the secret is not participant-scoped.

### "No activation (idPostfix) is available in the current XiansContext"

Same idea as above for `ActivationScope()` (no-arg). Use the explicit overload or omit it.

### "A secret with this key already exists"

Key is globally unique. Use a different key or update the existing secret by id.

### FetchByKeyAsync returns null

- No secret with that key, or
- Scope does not match (e.g. secret was created with `tenantId = "t1"` but you called `FetchByKeyAsync` with no tenant or a different tenant). Ensure the builder's scope matches the secret's scope.

### 403 / 404 from server

Check client certificate and that the agent is registered and allowed to use the Secret Vault API on the server.

## API Reference

### Entry points on `agent.Secrets`

| Method | Description |
|--------|-------------|
| `TenantScope()` | **Recommended starting point.** Returns a tenant-scoped builder using the tenant id from `XiansContext` (or the agent's certificate tenant). Throws if no tenant id can be resolved. |
| `TenantScope(string tenantId)` | Returns a tenant-scoped builder with an explicit tenant id. |
| `ScopeUnbound()` | Returns a builder with all four scope dimensions set to `null` (admin / cross-tenant flows). |
| `Scope()` | Alias for `TenantScope()`. Kept for backwards compatibility. |

### Builder narrowing setters

| Method | Description |
|--------|-------------|
| `TenantScope(string? tenantId)` | Override the tenant scope (`null` = cross-tenant). |
| `AgentScope()` | Narrow to the **current agent** (`XiansContext.SafeAgentName` ?? this agent's `Name`). |
| `AgentScope(string? agentId)` | Set agent scope explicitly (`null` = all agents). |
| `ParticipantScope()` | Narrow to the **current participant** (`XiansContext.SafeParticipantId`). Throws if not in context. |
| `ParticipantScope(string? participantId)` | Set participant scope explicitly (`null` = any participant). |
| `UserScope(string? userId)` | Legacy alias for `ParticipantScope(string?)`. |
| `ActivationScope()` | Narrow to the **current activation** (`XiansContext.SafeIdPostfix`). Throws if not in context. |
| `ActivationScope(string? activationName)` | Set activation scope explicitly (`null` = any activation). |

### CRUD methods

| Method | Description |
|--------|-------------|
| `CreateAsync(key, value, additionalData?, ct)` | Create secret. Returns `SecretVaultGetResponse`. |
| `FetchByKeyAsync(key, ct)` | Fetch by key (strict scope). Returns `SecretVaultFetchResponse?`. |
| `ListAsync(ct)` | List secrets (filtered by current tenant / agent / activationName). Returns `List<SecretVaultListItem>`. |
| `GetByIdAsync(id, ct)` | Get full secret by id. Returns `SecretVaultGetResponse?`. |
| `UpdateAsync(id, value?, additionalData?, tenantId?, agentId?, userId?, activationName?, ct)` | Update by id. Returns `SecretVaultGetResponse`. |
| `DeleteAsync(id, ct)` | Delete by id. Returns `bool`. |

## Summary

- **Tenant-first** – `agent.Secrets.TenantScope()` is the natural starting point and covers the most common case.
- **Narrow as needed** – Chain `.AgentScope()` → `.ParticipantScope()` → `.ActivationScope()`. Each has a no-arg overload (auto from context) and a `(string?)` overload (explicit / `null` to broaden).
- **Escape hatch** – `agent.Secrets.ScopeUnbound()` for admin / cross-tenant flows.
- **CRUD** – Create (key + value), FetchByKey (value + additionalData), List, GetById, Update, Delete.
- **Strict scope** – Fetch-by-key matches tenant / agent / participant / activation exactly.
- **Server** – Values encrypted at rest; Agent API at `api/agent/secrets` with client certificate.

Use Secret Vault for API keys, webhook secrets, and other sensitive key-value data scoped by tenant, agent, participant, or activation.
