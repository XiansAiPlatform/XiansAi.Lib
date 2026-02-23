# Secret Vault

> **TL;DR**: Secret Vault is a secure key-value store for secrets (API keys, tokens, webhook secrets). Use a **builder pattern** to set scope (tenant, agent, user) then perform CRUD. Values are encrypted at rest on the server.

## What Is Secret Vault?

Secret Vault stores sensitive key-value pairs with optional scoping:

- **Key** – Unique name (e.g. `api-key`, `webhook-secret`)
- **Value** – The secret (encrypted at rest with AES-256-GCM on the server)
- **Scope** – Optional tenant, agent, and user for access control
- **AdditionalData** – Optional flat metadata (string/number/boolean only; not for secrets)

### Scope Semantics

| Scope     | Meaning when set        | Meaning when null        |
|----------|--------------------------|--------------------------|
| TenantId | Secret only for that tenant | Cross-tenant (any tenant) |
| AgentId  | Secret only for that agent  | Across all agents          |
| UserId   | Only that user can access   | Any user may access       |

**Fetch-by-key** uses **strict** scope matching: the request must send the same scope values as stored. For example, a secret stored with `tenantId = "acme"` will not be returned if you fetch without `tenantId` or with a different tenant.

## Quick Start

```csharp
using Xians.Lib.Agents.Secrets;
using Xians.Lib.Agents.Secrets.Models;

// 1. Get a scoped builder (tenant + agent + user)
var secrets = agent.Secrets.Scope()
    .TenantScope("tenant-1")
    .AgentScope("my-agent")
    .UserScope("user-1");

// 2. Create a secret
var created = await secrets.CreateAsync("api-key", "sk-xxx");

// 3. Fetch by key (same scope)
var fetched = await secrets.FetchByKeyAsync("api-key");
Console.WriteLine(fetched?.Value); // "sk-xxx"

// 4. List, update, delete
var list = await secrets.ListAsync();
await secrets.UpdateAsync(created.Id, value: "sk-new");
await secrets.DeleteAsync(created.Id);
```

## Key Features

- **Builder pattern** – Chain `TenantScope()`, `AgentScope()`, `UserScope()` then CRUD
- **Full CRUD** – Create, fetch by key, list, get by id, update, delete
- **Strict scope** – Fetch-by-key matches scope exactly (tenant/agent/user)
- **Encrypted at rest** – Server encrypts values; lib only sends/receives plaintext over TLS
- **Optional metadata** – `additionalData` for flat key-value (env, service name, etc.); not for sensitive data

## Implementation Overview

### Components in Xians.Lib

| Component | Location | Role |
|-----------|----------|------|
| **SecretVaultCollection** | `Agents/Secrets/SecretVaultCollection.cs` | Entry point on `agent.Secrets`; exposes `Scope()` and convenience scope methods |
| **SecretVaultScopeBuilder** | `Agents/Secrets/SecretVaultScopeBuilder.cs` | Fluent scope + CRUD (Create, FetchByKey, List, GetById, Update, Delete) |
| **Models** | `Agents/Secrets/Models/SecretVaultModels.cs` | DTOs: create/update requests, get/fetch/list responses |

### Server API

The lib calls the **Agent API** at `api/agent/secrets` (client certificate auth). Same backend as the Admin Secret Vault API; see the server’s `SECRET_VAULT.md` for encryption, validation, and database details.

## Usage

### 1. Getting a scope builder

Start from `agent.Secrets`, then either use the generic builder or a convenience method:

```csharp
// Generic: start with no scope (or tenant from context via Scope())
var builder = agent.Secrets.Scope();

// Set scope fluently
builder.TenantScope("tenant-1").AgentScope("agent-1").UserScope("user-1");

// Convenience: tenant only
var byTenant = agent.Secrets.TenantScope("tenant-1");

// Convenience: tenant + agent
var byTenantAgent = agent.Secrets.TenantScope("tenant-1", "agent-1");

// Convenience: full scope in one call
var full = agent.Secrets.WithScope("tenant-1", "agent-1", "user-1");
```

`Scope()` resolves tenant from `XiansContext` when available (e.g. in workflows); otherwise from agent options (e.g. certificate tenant). You can override by chaining `TenantScope(...)`.

### 2. Create

Creates a secret. **Key must be unique** in the vault.

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1").AgentScope("a1");

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
var scoped = agent.Secrets.Scope().TenantScope("t1").AgentScope("a1");
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

Lists secrets with optional tenant/agent filter (from current scope). Does **not** return the secret value.

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
var items = await scoped.ListAsync();

foreach (var item in items)
{
    Console.WriteLine($"{item.Key} ({item.Id})");
    // item has: Id, Key, TenantId, AgentId, UserId, AdditionalData, CreatedAt, CreatedBy
}
```

### 5. Get by id

Returns full record including decrypted value. Use when you already have the secret id (e.g. from create or list).

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
var secret = await scoped.GetByIdAsync("507f1f77bcf86cd799439011");

if (secret != null)
{
    Console.WriteLine($"{secret.Key}: {secret.Value}");
}
```

### 6. Update

Updates an existing secret by id. Omitted parameters leave existing values unchanged.

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");

// Update value only
await scoped.UpdateAsync(id, value: "new-secret-value");

// Update value and metadata
await scoped.UpdateAsync(id, value: "new-value", additionalData: new { rev = 2 });

// Update scope (pass explicit tenant/agent/user to change scope)
await scoped.UpdateAsync(id, tenantId: "t2", agentId: "a2", userId: "u2");
```

### 7. Delete

Deletes a secret by id. Returns `true` if deleted, `false` if not found.

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
bool deleted = await scoped.DeleteAsync(id);
```

## Common Patterns

### Per-tenant API key

```csharp
var tenantScoped = agent.Secrets.TenantScope(tenantId);
await tenantScoped.CreateAsync("external-api-key", apiKeyFromConfig);
// Later, in a request for that tenant:
var key = await agent.Secrets.TenantScope(tenantId).FetchByKeyAsync("external-api-key");
```

### Per-agent + per-user secret

```csharp
var scoped = agent.Secrets
    .Scope()
    .TenantScope("t1")
    .AgentScope(agent.Name)
    .UserScope(userId);
await scoped.CreateAsync("user-token", token);
var token = await scoped.FetchByKeyAsync("user-token");
```

### List then get value for one

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
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

- **SecretVaultCreateRequest** – Key, Value, TenantId?, AgentId?, UserId?, AdditionalData?
- **SecretVaultUpdateRequest** – Value?, TenantId?, AgentId?, UserId?, AdditionalData?
- **SecretVaultGetResponse** – Id, Key, Value, TenantId, AgentId, UserId, AdditionalData, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
- **SecretVaultFetchResponse** – Value, AdditionalData (fetch-by-key only)
- **SecretVaultListItem** – Id, Key, TenantId, AgentId, UserId, AdditionalData, CreatedAt, CreatedBy (no Value)

### AdditionalData rules (server)

- Flat object only; values must be **string**, **number**, or **boolean**
- Keys: `a-zA-Z0-9_.-`, max 128 chars; max 50 keys; total size ≤ 8 KB
- Not for sensitive data; stored as sanitized JSON

## Requirements

- **HTTP service** – Agent must be configured with `IHttpClientService` (e.g. via `XiansPlatform` with server URL and client certificate). Otherwise, Secret Vault methods throw `InvalidOperationException`.
- **Agent API auth** – Server expects client certificate auth for `api/agent/secrets`; the lib uses the same HTTP client as Knowledge/Documents.

## Best Practices

- **Do** use clear, unique keys (e.g. `tenant-{id}-api-key`, `webhook-{service}`).
- **Do** use the same scope when creating and fetching (strict match).
- **Do** use `additionalData` only for non-sensitive metadata (env, service name).
- **Do** handle `null` from `FetchByKeyAsync` and `GetByIdAsync` (not found or access denied).
- **Don’t** store highly sensitive material in `additionalData` (it is not encrypted like the value).
- **Don’t** forget to set scope when the secret is tenant/agent/user-specific; otherwise you may create or fetch cross-tenant secrets.

## Troubleshooting

### "HTTP service is not configured"

Agent was not initialized with an HTTP client (e.g. not via `XiansPlatform` with ServerUrl/certificate). Use the same setup as for Knowledge/Documents.

### "A secret with this key already exists"

Key is globally unique. Use a different key or update the existing secret by id.

### FetchByKeyAsync returns null

- No secret with that key, or
- Scope does not match (e.g. secret was created with `tenantId = "t1"` but you called `FetchByKeyAsync` with no tenant or a different tenant). Ensure the builder’s scope matches the secret’s scope.

### 403 / 404 from server

Check client certificate and that the agent is registered and allowed to use the Secret Vault API on the server.

## API Reference (Scope Builder)

After `agent.Secrets.Scope()` or `TenantScope(...)` / `WithScope(...)`:

| Method | Description |
|--------|-------------|
| `TenantScope(tenantId)` | Set tenant scope (null = cross-tenant). Returns builder. |
| `AgentScope(agentId)` | Set agent scope (null = all agents). Returns builder. |
| `UserScope(userId)` | Set user scope (null = any user). Returns builder. |
| `CreateAsync(key, value, additionalData?, ct)` | Create secret. Returns `SecretVaultGetResponse`. |
| `FetchByKeyAsync(key, ct)` | Fetch by key (strict scope). Returns `SecretVaultFetchResponse?`. |
| `ListAsync(ct)` | List secrets (filtered by current tenant/agent). Returns `List<SecretVaultListItem>`. |
| `GetByIdAsync(id, ct)` | Get full secret by id. Returns `SecretVaultGetResponse?`. |
| `UpdateAsync(id, value?, additionalData?, tenantId?, agentId?, userId?, ct)` | Update by id. Returns `SecretVaultGetResponse`. |
| `DeleteAsync(id, ct)` | Delete by id. Returns `bool`. |

## Summary

- **Builder pattern** – `agent.Secrets.Scope().TenantScope(...).AgentScope(...).UserScope(...)` then CRUD.
- **Scopes** – `TenantScope()`, `AgentScope()`, `UserScope()`; null means “any” for that dimension.
- **CRUD** – Create (key + value), FetchByKey (value + additionalData), List, GetById, Update, Delete.
- **Strict scope** – Fetch-by-key matches tenant/agent/user exactly.
- **Server** – Values encrypted at rest; Agent API at `api/agent/secrets` with client certificate.

Use Secret Vault for API keys, webhook secrets, and other sensitive key-value data scoped by tenant, agent, or user.
