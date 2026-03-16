# Secret Vault Scope Validation (Lib)

This document describes the **client-side scope validation** in the Xians.Lib Secret Vault: how the SDK ensures that the scope used for Secret Vault operations matches the current message/workflow context (tenant, user, agent, activation).

## Overview

When your code runs **inside a Temporal workflow or activity**, the Secret Vault scope builder validates that any scope you set (tenantId, userId, agentId, activationName) matches the current execution context. This prevents a workflow from accidentally or maliciously accessing secrets in another tenant, for another user, or for another agent/activation.

- **When validation runs**: At the start of every Secret Vault operation (Create, FetchByKey, List, UpdateByKey, DeleteByKey) that uses `SecretVaultScopeBuilder`.
- **When validation is skipped**: When the code is **not** in a workflow or activity (e.g. unit tests, local mode, or code outside Temporal). In those cases, no scope-vs-context check is performed.

## What is validated

The builder holds optional scope values: `_tenantId`, `_agentId`, `_userId`, `_activationName`. For each of these that is **set** (non-null, non-empty), and for which the **context** also has a value, they must **match**. Mismatch causes an `InvalidOperationException` before any HTTP call.

| Scope field    | Compared against (from `XiansContext`) |
|----------------|----------------------------------------|
| **tenantId**   | `XiansContext.SafeTenantId`            |
| **agentId**   | `XiansContext.SafeAgentName` or `_agent.Name` |
| **userId**    | `XiansContext.SafeParticipantId`      |
| **activationName** | `XiansContext.SafeIdPostfix`     |

- If the **builder** has a value and **context** has a value and they differ → **exception**.
- If the builder has a value and context has no value (e.g. null) → no exception (validation is only “both present ⇒ must match”).
- If the builder has no value → nothing to validate for that field.

So: you cannot use the builder to target a **different** tenant, user, agent, or activation than the current message/workflow context when both are present.

## Implementation

### Where it lives

- **Class**: `SecretVaultScopeBuilder` in `Xians.Lib.Agents.Secrets`  
- **Method**: `ValidateScopeAgainstMessageContext()` (private), called at the start of:
  - `CreateAsync`
  - `FetchByKeyAsync`
  - `ListAsync`
  - `UpdateByKeyAsync`
  - `DeleteByKeyAsync`

### When it runs

```csharp
if (!XiansContext.InWorkflowOrActivity)
    return;
```

So validation runs only when `XiansContext.InWorkflowOrActivity` is true (i.e. inside a Temporal workflow or activity). Outside that (e.g. tests, console app without workflow), no validation is performed.

### Logic (conceptual)

For each of tenantId, agentId, userId, activationName:

1. If the builder’s value is null or empty → skip.
2. If the context value is null or empty → skip (no comparison).
3. Otherwise, if builder value ≠ context value → throw `InvalidOperationException` with a clear message, e.g.  
   *"Secret Vault tenantId scope 'X' does not match message context tenant 'Y'."*

Context values are read via the **Safe** / non-throwing getters so that missing context does not throw; only an explicit mismatch does.

## Example behaviour

**Scenario 1 – In a workflow for tenant `t1`, user `u1`, agent `Secrets Agent`**

- `Scope().TenantScope("t1").UserScope("u1")` → matches context → no exception.
- `Scope().TenantScope("t2")` → context has tenant `t1` → **exception**: tenantId scope does not match message context tenant.

**Scenario 2 – Unit test (no workflow/activity)**

- `Scope().TenantScope("any")` → `InWorkflowOrActivity` is false → validation skipped → no exception (server may still enforce tenant later).

**Scenario 3 – Activation (idPostfix) set**

- Workflow context has `SafeIdPostfix` = `"activation-1"`.
- `ActivationScope("activation-1")` → matches.
- `ActivationScope("activation-2")` → **exception**: activationName scope does not match message context activation.

## Exception messages

You may see:

- *"Secret Vault tenantId scope '{scope}' does not match message context tenant '{context}'."*
- *"Secret Vault agentId scope '{scope}' does not match message context agent '{context}'."*
- *"Secret Vault userId scope '{scope}' does not match message context user '{context}'."*
- *"Secret Vault activationName scope '{scope}' does not match message context activation '{context}'."*

These indicate that the scope passed to the builder does not match the current execution context and the call is rejected before any request is sent.

## Design notes

- **Defence in depth**: The server also enforces tenant (and possibly scope) by role (e.g. SysAdmin vs TenantAdmin). The lib validation adds a client-side check so that misuse or bugs in workflow code are caught early and cannot even send a request for another scope.
- **Safe getters**: Context is read with `SafeTenantId`, `SafeParticipantId`, `SafeAgentName`, `SafeIdPostfix` so that missing or partial context does not cause unrelated exceptions; only a clear scope-vs-context mismatch does.
- **Activation**: Activation context is represented by `XiansContext.SafeIdPostfix` (e.g. workflow/activation identifier). If your product uses a different notion of “activation name”, the comparison is still “builder activation name vs context activation value”; the current implementation uses idPostfix as that value.

## Summary

| Aspect | Detail |
|--------|--------|
| **Purpose** | Ensure Secret Vault scope matches message/workflow context (tenant, user, agent, activation). |
| **When** | Start of every scope-builder CRUD call, and only when `XiansContext.InWorkflowOrActivity` is true. |
| **Checked** | tenantId, agentId, userId, activationName vs `SafeTenantId`, `SafeAgentName`/agent name, `SafeParticipantId`, `SafeIdPostfix`. |
| **On mismatch** | `InvalidOperationException` with a clear message; no HTTP request is made. |
| **Skipped** | When not in workflow or activity (e.g. tests, local mode). |

For general Secret Vault usage, scoping, and API, see [SecretVault.md](./SecretVault.md).
