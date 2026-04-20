# Secret Vault

> **TL;DR**: Secret Vault is a secure key-value store for secrets (API keys, tokens, webhook secrets). Use a **builder pattern** to set scope (tenant, agent, user, activation) then perform CRUD. Values are encrypted at rest on the server.

## Security Note

> **Breaking change (key+scope-only operations)**: Id-based methods (`GetByIdAsync`, `UpdateAsync(id, ...)`, `DeleteAsync(id)`) have been **removed**. All secrets are now identified by **key + scope**. Use `UpdateByKeyAsync` and `DeleteByKeyAsync` instead. This eliminates the ability to look up or mutate a secret using only an opaque id, which was a server-side security vulnerability.

## What Is Secret Vault?

Secret Vault stores sensitive key-value pairs with optional scoping:

- **Key** – Unique name (e.g. `api-key`, `webhook-secret`)
- **Value** – The secret (encrypted at rest with AES-256-GCM on the server)
- **Scope** – Optional tenant, agent, user, and activation for access control
- **AdditionalData** – Optional flat metadata (string/number/boolean only; not for secrets)

### Scope Semantics

| Scope          | Meaning when set                    | Meaning when null                    |
|----------------|-------------------------------------|--------------------------------------|
| TenantId       | Secret only for that tenant         | Cross-tenant (any tenant). When using `Scope()`, if you do not call `TenantScope(...)`, the tenant is taken from `XiansContext.SafeTenantId` or the agent's certificate tenant; system-scoped agents with no tenant in context get `null` (cross-tenant). |
| AgentId        | Secret only for that agent          | Across all agents                     |
| UserId         | Only that user can access           | Any user may access                   |
| ActivationName | Only that agent activation can access | Any activation of the agent can access |

**Fetch-by-key** uses **strict** scope matching for all scopes (tenant, agent, user, activation): the request must send the same scope values as stored. If you omit a scope (e.g. no `activationName`), only secrets with that scope null are returned; if you send a scope value, the document must have that exact value.

## Quick Start

```csharp
using Xians.Lib.Agents.Secrets;
using Xians.Lib.Agents.Secrets.Models;

// 1. Get a scoped builder (tenant + agent + user + optional activation)
var secrets = agent.Secrets.Scope()
    .TenantScope("tenant-1")
    .AgentScope("my-agent")
    .UserScope("user-1")
    .ActivationScope("my-activation");  // optional: null = any activation can access

// 2. Create a secret
var created = await secrets.CreateAsync("api-key", "sk-xxx");

// 3. Fetch by key (same scope)
var fetched = await secrets.FetchByKeyAsync("api-key");
Console.WriteLine(fetched?.Value); // "sk-xxx"

// 4. Update by key (same scope)
await secrets.UpdateByKeyAsync("api-key", value: "sk-new");

// 5. Delete by key (same scope)
await secrets.DeleteByKeyAsync("api-key");
```

## Key Features

- **Builder pattern** – Chain `TenantScope()`, `AgentScope()`, `UserScope()`, `ActivationScope()` then CRUD
- **Full CRUD** – Create, fetch by key, list, update by key, delete by key
- **Key+scope identity** – Secrets are uniquely identified by key + scope; no id-based operations are exposed
- **Strict scope** – Fetch-by-key matches scope exactly for all dimensions (tenant, agent, user, activation)
- **Encrypted at rest** – Server encrypts values; lib only sends/receives plaintext over TLS
- **Optional metadata** – `additionalData` for flat key-value (env, service name, etc.); not for sensitive data

## Implementation Overview

### Components in Xians.Lib

| Component | Location | Role |
|-----------|----------|------|
| **SecretVaultCollection** | `Agents/Secrets/SecretVaultCollection.cs` | Entry point on `agent.Secrets`; exposes `Scope()` and convenience scope methods |
| **SecretVaultScopeBuilder** | `Agents/Secrets/SecretVaultScopeBuilder.cs` | Fluent scope + CRUD (Create, FetchByKey, List, UpdateByKey, DeleteByKey) |
| **Models** | `Agents/Secrets/Models/SecretVaultModels.cs` | DTOs: create/update requests, get/fetch/list responses |

### Server API

The lib calls the **Agent API** at `api/agent/secrets` (client certificate auth). Same backend as the Admin Secret Vault API; see the server's `SECRET_VAULT.md` for encryption, validation, and database details.

## Usage

### 1. Getting a scope builder

Start from `agent.Secrets`, then either use the generic builder or a convenience method:

```csharp
// 1) Generic: start with no explicit tenant scope.
//    Scope() will:
//    - Use XiansContext.SafeTenantId when available (typical in workflows), or
//    - Fall back to the agent's certificate tenant for non-system-scoped agents, or
//    - Use null (cross-tenant) for system-scoped agents with no tenant in context.
var fromContextOrCert = agent.Secrets.Scope();

// 2) Explicit tenant scope: secret is only for that tenant.
var explicitTenant = agent.Secrets.Scope()
    .TenantScope("tenant-1");

// 3) Explicit cross-tenant secret: TenantScope(null) sends tenantId: null (no tenant scope).
var crossTenant = agent.Secrets.Scope()
    .TenantScope(null);

// Further refine scope:
var builder = agent.Secrets.Scope()
    .TenantScope("tenant-1")
    .AgentScope("agent-1")
    .UserScope("user-1")
    .ActivationScope("activation-1");

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
    // item has: Id, Key, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy
}
```

### 5. Update by key

Updates an existing secret by key and scope. Omitted parameters leave existing values unchanged. The scope in the request must match the scope of the stored secret (the server enforces uniqueness on key + scope).

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");

// Update value only (scope is taken from the builder)
await scoped.UpdateByKeyAsync("my-key", value: "new-secret-value");

// Update value and metadata
await scoped.UpdateByKeyAsync("my-key", value: "new-value", additionalData: new { rev = 2 });

// Override scope for the update call (must match the stored secret's scope)
await scoped.UpdateByKeyAsync("my-key", tenantId: "t1", agentId: "a1", userId: "u1");
```

Throws `InvalidOperationException` if the secret is not found.

### 6. Delete by key

Deletes a secret by key and scope. Returns `true` if deleted, `false` if not found.

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
bool deleted = await scoped.DeleteByKeyAsync("my-key");
```

The delete request sends the key plus any scope values set on the builder as query parameters. The server resolves the secret by key + scope and deletes it.

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

### Per-activation secret (one activation of an agent only)

```csharp
// Secret only for this activation (e.g. from XiansContext.SafeIdPostfix in a workflow)
var scoped = agent.Secrets.Scope()
    .TenantScope(tenantId)
    .AgentScope(agent.Name)
    .ActivationScope("my-activation");
await scoped.CreateAsync("activation-api-key", key);
var key = await scoped.FetchByKeyAsync("activation-api-key");
```

### List then fetch the value for one

To get the decrypted value of a specific secret after listing, call `FetchByKeyAsync` with the same scope used when the secret was created:

```csharp
var scoped = agent.Secrets.Scope().TenantScope("t1");
var list = await scoped.ListAsync();
var first = list.FirstOrDefault();
if (first != null)
{
    // Use the key (and same scope) to fetch the decrypted value
    var fetched = await scoped.FetchByKeyAsync(first.Key);
    Console.WriteLine(fetched?.Value);
}
```

## Data Models

### Request/response types (lib)

- **SecretVaultCreateRequest** – Key (required), Value (required), TenantId?, AgentId?, UserId?, ActivationName?, AdditionalData?
- **SecretVaultUpdateRequest** – Key (required), Value?, TenantId?, AgentId?, UserId?, ActivationName?, AdditionalData?
- **SecretVaultGetResponse** – Id, Key, Value, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
- **SecretVaultFetchResponse** – Value, AdditionalData (fetch-by-key only)
- **SecretVaultListItem** – Id, Key, TenantId, AgentId, UserId, ActivationName, AdditionalData, CreatedAt, CreatedBy (no Value)

### AdditionalData rules (server)

- Flat object only; values must be **string**, **number**, or **boolean**
- Keys: `a-zA-Z0-9_.-`, max 128 chars; max 50 keys; total size ≤ 8 KB
- Not for sensitive data; stored as sanitized JSON

## Requirements

- **HTTP service** – Agent must be configured with `IHttpClientService` (e.g. via `XiansPlatform` with server URL and client certificate). Otherwise, Secret Vault methods throw `InvalidOperationException`.
- **Agent API auth** – Server expects client certificate auth for `api/agent/secrets`; the lib uses the same HTTP client as Knowledge/Documents.

## Best Practices

- **Do** use clear, unique keys (e.g. `tenant-{id}-api-key`, `webhook-{service}`).
- **Do** use the same scope when creating, fetching, updating, and deleting (strict match).
- **Do** use `additionalData` only for non-sensitive metadata (env, service name).
- **Do** handle `null` from `FetchByKeyAsync` (not found or access denied).
- **Don't** store highly sensitive material in `additionalData` (it is not encrypted like the value).
- **Don't** forget to set scope when the secret is tenant/agent/user-specific; otherwise you may create or fetch cross-tenant secrets.

## Migration from Id-Based Operations

If you were calling the now-removed id-based methods, migrate as follows:

| Removed method | Replacement |
|----------------|-------------|
| `GetByIdAsync(id)` | `FetchByKeyAsync(key)` — if you only need the decrypted value and additionalData. For full record metadata, call `ListAsync()` and match by key. |
| `UpdateAsync(id, ...)` | `UpdateByKeyAsync(key, ...)` — use the **same scope** as the stored secret. |
| `DeleteAsync(id)` | `DeleteByKeyAsync(key)` — use the **same scope** as the stored secret. |

The scope passed to `UpdateByKeyAsync` and `DeleteByKeyAsync` must match the scope under which the secret was originally created, because the server resolves the record by (key + scope) rather than by id.

## Troubleshooting

### "HTTP service is not configured"

Agent was not initialized with an HTTP client (e.g. not via `XiansPlatform` with ServerUrl/certificate). Use the same setup as for Knowledge/Documents.

### "A secret with this key already exists"

Key is globally unique within a scope. Use a different key or update the existing secret with `UpdateByKeyAsync`.

### FetchByKeyAsync returns null

- No secret with that key, or
- Scope does not match (e.g. secret was created with `tenantId = "t1"` but you called `FetchByKeyAsync` with no tenant or a different tenant). Ensure the builder's scope matches the secret's scope.

### UpdateByKeyAsync throws "Secret not found"

The key + scope combination did not match any stored secret. Verify that the scope (tenantId, agentId, userId, activationName) exactly matches the scope used when the secret was created.

### 403 / 404 from server

Check client certificate and that the agent is registered and allowed to use the Secret Vault API on the server.

## API Reference (Scope Builder)

After `agent.Secrets.Scope()` or `TenantScope(...)` / `WithScope(...)`:

| Method | Description |
|--------|-------------|
| `TenantScope(tenantId)` | Set tenant scope (null = cross-tenant). Returns builder. |
| `AgentScope(agentId)` | Set agent scope (null = all agents). Returns builder. |
| `UserScope(userId)` | Set user scope (null = any user). Returns builder. |
| `ActivationScope(activationName)` | Set activation scope (null = any activation of the agent). Returns builder. |
| `CreateAsync(key, value, additionalData?, ct)` | Create secret. Returns `SecretVaultGetResponse`. |
| `FetchByKeyAsync(key, ct)` | Fetch by key (strict scope). Returns `SecretVaultFetchResponse?`. |
| `ListAsync(ct)` | List secrets (filtered by current tenant/agent/activationName). Returns `List<SecretVaultListItem>`. |
| `UpdateByKeyAsync(key, value?, additionalData?, tenantId?, agentId?, userId?, activationName?, ct)` | Update by key + scope. Returns `SecretVaultGetResponse`. Throws if not found. |
| `DeleteByKeyAsync(key, ct)` | Delete by key + scope. Returns `bool` (true = deleted, false = not found). |

## Summary

- **Builder pattern** – `agent.Secrets.Scope().TenantScope(...).AgentScope(...).UserScope(...)` then CRUD.
- **Scopes** – `TenantScope()`, `AgentScope()`, `UserScope()`; null means "any" for that dimension.
- **CRUD** – Create (key + value), FetchByKey (value + additionalData), List, UpdateByKey, DeleteByKey.
- **Key+scope identity** – All operations use key + scope; id-based operations are not supported.
- **Strict scope** – Fetch-by-key matches tenant/agent/user/activation exactly (same as other scopes).
- **Server** – Values encrypted at rest; Agent API at `api/agent/secrets` with client certificate.

Use Secret Vault for API keys, webhook secrets, and other sensitive key-value data scoped by tenant, agent, user, or activation.
