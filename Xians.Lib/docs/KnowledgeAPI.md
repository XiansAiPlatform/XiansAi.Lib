# Knowledge API Requirements

This document outlines the server API endpoints required to support the Knowledge SDK in `Xians.Lib`.

## Overview

The Knowledge SDK provides agents with the ability to store, retrieve, update, and manage knowledge items (instructions, documents, etc.) scoped to specific agents and tenants.

## Required Server API Endpoints

All endpoints use the `X-Tenant-Id` header for tenant routing, which is critical for system-scoped agents.

### 1. Get Knowledge (Read)

**Endpoint:** `GET /api/agent/knowledge/latest`

**Query Parameters:**
- `name` (required): The name of the knowledge to retrieve
- `agent` (required): The agent name to scope the knowledge to

**Headers:**
- `X-Tenant-Id`: Tenant ID for routing (automatically added by SDK)
- `Authorization`: Bearer token (handled by HTTP client service)

**Response:**
- **200 OK**: Returns knowledge object as JSON
  ```json
  {
    "id": "string",
    "name": "string",
    "version": "string",
    "content": "string",
    "type": "string",
    "createdAt": "2024-01-01T00:00:00Z",
    "agent": "string",
    "tenantId": "string"
  }
  ```
- **404 Not Found**: Knowledge not found
- **400 Bad Request**: Invalid parameters
- **401 Unauthorized**: Authentication failed
- **403 Forbidden**: Access denied

**Security Considerations:**
- Validate that the requesting agent can only access its own knowledge
- Enforce tenant isolation using X-Tenant-Id header
- Validate input lengths (max 256 characters for name and agent)

---

### 2. Create/Update Knowledge (Write)

**Endpoint:** `POST /api/agent/knowledge`

**Headers:**
- `X-Tenant-Id`: Tenant ID for routing
- `Authorization`: Bearer token
- `Content-Type`: application/json

**Request Body:**
```json
{
  "name": "string (required)",
  "content": "string (required)",
  "type": "string (optional, e.g., 'instruction', 'document', 'json', 'markdown')",
  "agent": "string (required)",
  "tenantId": "string (optional, set from X-Tenant-Id header if not provided)"
}
```

**Response:**
- **200 OK**: Knowledge updated successfully
- **201 Created**: Knowledge created successfully
- **400 Bad Request**: Invalid request body
- **401 Unauthorized**: Authentication failed
- **403 Forbidden**: Access denied

**Behavior:**
- If knowledge with the same `name` and `agent` exists, update it
- If it doesn't exist, create new knowledge
- Automatically set `tenantId` from `X-Tenant-Id` header
- Set `createdAt` to current timestamp

**Security Considerations:**
- Validate that agent can only create/update its own knowledge
- Enforce tenant isolation
- Validate content size (recommend max 10MB)
- Sanitize inputs to prevent injection attacks

---

### 3. Delete Knowledge

**Endpoint:** `DELETE /api/agent/knowledge`

**Query Parameters:**
- `name` (required): The name of the knowledge to delete
- `agent` (required): The agent name

**Headers:**
- `X-Tenant-Id`: Tenant ID for routing
- `Authorization`: Bearer token

**Response:**
- **200 OK**: Knowledge deleted successfully
- **404 Not Found**: Knowledge not found (treat as already deleted)
- **400 Bad Request**: Invalid parameters
- **401 Unauthorized**: Authentication failed
- **403 Forbidden**: Access denied

**Security Considerations:**
- Validate that agent can only delete its own knowledge
- Enforce tenant isolation
- Consider soft delete vs hard delete based on requirements

---

### 4. List Knowledge (NEW - Required)

**Endpoint:** `GET /api/agent/knowledge/list`

**Query Parameters:**
- `agent` (required): The agent name to list knowledge for

**Headers:**
- `X-Tenant-Id`: Tenant ID for routing
- `Authorization`: Bearer token

**Response:**
- **200 OK**: Returns array of knowledge objects
  ```json
  [
    {
      "id": "string",
      "name": "string",
      "version": "string",
      "content": "string",
      "type": "string",
      "createdAt": "2024-01-01T00:00:00Z",
      "agent": "string",
      "tenantId": "string"
    }
  ]
  ```
- **400 Bad Request**: Invalid parameters
- **401 Unauthorized**: Authentication failed
- **403 Forbidden**: Access denied

**Security Considerations:**
- Validate that agent can only list its own knowledge
- Enforce tenant isolation
- Consider pagination for large result sets (optional enhancement)

---

## Existing Endpoints Status

Based on the old `XiansAi.Lib.Src` implementation:

1. ✅ **GET /api/agent/knowledge/latest** - Already exists
2. ✅ **POST /api/agent/knowledge** - Already exists
3. ❓ **DELETE /api/agent/knowledge** - May need to be added
4. ❓ **GET /api/agent/knowledge/list** - May need to be added

## Tenant Isolation

All endpoints MUST enforce tenant isolation:

1. Extract tenant ID from `X-Tenant-Id` header
2. Scope all queries to that tenant
3. For system-scoped agents, the tenant ID comes from the workflow context
4. For non-system-scoped agents, validate that the tenant ID matches the agent's registered tenant

## Authentication & Authorization

- All endpoints require valid Bearer token in `Authorization` header
- Validate that the authenticated agent matches the `agent` parameter
- Prevent agents from accessing other agents' knowledge

## Data Model

The server should store knowledge with the following schema:

```sql
-- Example schema (adapt to your database)
CREATE TABLE knowledge (
    id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    version VARCHAR(50),
    content TEXT NOT NULL,
    type VARCHAR(50),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    agent VARCHAR(256) NOT NULL,
    tenant_id VARCHAR(256) NOT NULL,
    UNIQUE KEY unique_knowledge (name, agent, tenant_id),
    INDEX idx_agent_tenant (agent, tenant_id)
);
```

## Rate Limiting

Consider implementing rate limiting on knowledge endpoints to prevent abuse:
- Recommend: 100 requests per minute per agent
- Higher limits for list operations may be needed

## Caching

The SDK does NOT implement caching (for consistency). The server may implement:
- Short-lived cache (5 minutes) for GET operations
- Cache invalidation on POST/DELETE operations

## Error Handling

All endpoints should return consistent error format:

```json
{
  "error": "Error message",
  "code": "ERROR_CODE",
  "details": {}
}
```

## Testing Checklist for Server Implementation

- [ ] GET endpoint returns correct knowledge for agent/tenant
- [ ] GET endpoint returns 404 for non-existent knowledge
- [ ] POST endpoint creates new knowledge
- [ ] POST endpoint updates existing knowledge
- [ ] DELETE endpoint removes knowledge
- [ ] DELETE endpoint returns 404 for non-existent knowledge
- [ ] LIST endpoint returns all knowledge for agent/tenant
- [ ] Tenant isolation is enforced (agent A cannot access agent B's knowledge)
- [ ] System-scoped agents can access knowledge across tenants correctly
- [ ] Authentication is required for all endpoints
- [ ] Input validation prevents injection attacks
- [ ] Content size limits are enforced

