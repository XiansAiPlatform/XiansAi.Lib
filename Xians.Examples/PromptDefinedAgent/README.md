# Prompt Defined Agent

A reusable Xians agent configured through Agent Studio. Each activation can have its own prompt and MCP tools without code changes.

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

## Scheduled prompts

Ask the agent to run a prompt on a recurring schedule, for example: `Every day at 9 AM Asia/Colombo, summarize Reuters, BBC, and TechCrunch in five bullets.` Each schedule stores its own prompt and parameters, appears under **Schedules** in Agent Studio, and sends results back to the requesting participant. The agent can list, reschedule, and delete existing schedules.

## MCP options

Server fields:

- `name` — unique display name for the MCP server.
- `url` — absolute HTTP or HTTPS MCP endpoint.
- `enabled` — enables the server; defaults to `true`.
- `transport` — connection transport; defaults to `auto`.
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

Secret lookup order is activation → agent → tenant. Invalid or unavailable MCP servers are skipped; built-in tools remain available.
