# Prompt Defined Agent

A reusable Xians agent configured through Agent Studio. Each activation can have its own prompt and MCP tools without code changes.

Tool calls and results from chat and scheduled runs appear in Agent Studio's tool timeline with readable labels. Details retain exact tool names and call IDs, but omit arguments and result payloads to avoid exposing sensitive data.

## Setup

Copy `.env.example` to `.env` and set:

```text
XIANS_SERVER_URL=
XIANS_API_KEY=
OPENAI_API_KEY=
```

Run from the solution directory:

```bash
dotnet run --project Xians.Examples/PromptDefinedAgent
```

## Configure an activation

In Agent Studio, open **Knowledge** and create an activation-level override for:

- `system-prompt` — instructions that define the agent's behavior.
- `Rules` — MCP servers whose tools the agent may use.

Example `Rules`:

```json
{
  "mcpServers": [
    {
      "name": "example",
      "url": "https://example.com/mcp",
      "enabled": true,
      "transport": "auto",
      "authentication": {
        "type": "bearer",
        "secret": "EXAMPLE_MCP_TOKEN"
      }
    }
  ]
}
```

## Xians MCP

Add this server to `Rules` (replace the URL when not running locally):

```json
{
  "mcpServers": [
    {
      "name": "xians",
      "url": "http://localhost:5005/api/v1/admin/mcp",
      "enabled": true,
      "transport": "streamableHttp",
      "context": "xians",
      "authentication": {
        "type": "bearer",
        "secret": "XIANS_MCP_KEY"
      }
    }
  ]
}
```

Generate an admin API key in **Developer → Secrets → Admin API Keys**, then save its value as `XIANS_MCP_KEY` in **Settings → Secrets**. The key must authenticate the agent's tenant; never put its value in Rules. Keep existing third-party servers in the same `mcpServers` array.

`context: "xians"` is client-only: the agent injects its tenant, agent, and activation into recognized Xians tools and hides those fields from the model. Omit it for third-party servers. Copy the server URL from **Settings → Connections → Xians MCP**; previous activation-scoped URLs are no longer supported.

## Scheduled prompts

Scheduling tools come exclusively from the **Xians MCP**; configure it in `Rules` to create, list, reschedule, delete, pause, or resume schedules. There are no built-in scheduling tools.

The agent discovers registered workflows using `list_workflows` and includes the current chat's participant and scope in scheduled prompt inputs, so results return to that conversation. Specify your timezone when requesting a schedule.

Xians MCP also supports Data Explorer: discover types, browse records, save JSON objects, and delete records after explicit confirmation. Ask “Save this report in Data Explorer under Reports”; chat replies are not automatically saved.

The agent worker still runs `Prompt Defined Agent:Scheduled Prompt Workflow`. Its input contains `Prompt`, `Parameters`, `ParticipantId`, and optional `Scope`; results go to the specified participant. Schedules appear under **Schedules** in Agent Studio.

The scheduled workflow is excluded from the activation wizard; chat-created schedules supply its required request.

## MCP options

Server fields:

- `name` — unique display name for the MCP server.
- `url` — absolute HTTP or HTTPS MCP endpoint.
- `enabled` — enables the server; defaults to `true`.
- `transport` — connection transport; defaults to `auto`.
- `context: "xians"` — injects the current tenant, agent, and activation into recognized Xians tools; these fields are hidden from the model. Omit for third-party servers.
- `authentication` — optional authentication configuration.

Transport values:

- `auto` — tries Streamable HTTP, then falls back to SSE.
- `streamableHttp` — uses the recommended remote MCP transport.
- `sse` — uses the legacy Server-Sent Events transport.

Authentication values:

- `none` — sends no authentication credentials.
- `bearer` — sends the secret named by `secret` as an HTTP bearer token.
- `apiKey` — sends the secret named by `secret` in `header`, which defaults to `X-API-Key`.
- `basic` — builds HTTP Basic authentication from `usernameSecret` and `passwordSecret`.

Authentication shapes:

```json
{ "type": "none" }
{ "type": "bearer", "secret": "TOKEN_SECRET_NAME" }
{ "type": "apiKey", "secret": "API_KEY_SECRET_NAME", "header": "X-API-Key" }
{ "type": "basic", "usernameSecret": "USERNAME_SECRET_NAME", "passwordSecret": "PASSWORD_SECRET_NAME" }
```

## Secrets

In Agent Studio, open **Settings → Secrets** and save each PAT, token, or password under a key such as `GITHUB_MCP_TOKEN`. Put only that key in `Rules` (for example, `"secret": "GITHUB_MCP_TOKEN"`), never the credential itself. Studio currently creates tenant-scoped secrets; values are encrypted at rest and hidden after saving.

Secret lookup order is activation → agent → tenant. Invalid or unavailable MCP servers are skipped; only tools from successfully connected MCP servers are available.

Rules and successful MCP connections are cached per activation for the worker lifetime. Restart the agent after changing Rules or referenced secrets.

## MCP diagnostics

Agent terminal logs show the activation, configured/disabled servers, connection stages, per-server tool counts, and total tools. Failures include their stage, exception type, and HTTP status when available. Credentials, endpoint URLs, exception messages, and tool payloads are not logged.
