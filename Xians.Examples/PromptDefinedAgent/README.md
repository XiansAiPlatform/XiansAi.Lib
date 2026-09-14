# Prompt Defined Agent

A generic Xians agent whose behavior is controlled by a `system-prompt` knowledge item.

## Capabilities

- Free-form conversations with recent message history.
- Activation-specific prompt overrides.
- Current date and time.
- Web search and web-page reading through Tavily.
- MCP tools discovered from activation-specific configuration.
- Markdown responses in Agent Studio.

## Setup

Copy `.env.example` to `.env` and configure:

```env
XIANS_SERVER_URL=
XIANS_API_KEY=
OPENAI_API_KEY=
TAVILY_API_KEY=
```

The default prompt is in `knowledge/system-prompt.md`. To give an instance a different prompt, create an activation-level knowledge override named `system-prompt` in Agent Studio.

The default MCP configuration is the `Rules` JSON knowledge item. Override it for an activation in Agent Studio:

```json
{
  "mcpServers": [
    {
      "name": "example",
      "url": "https://example.com/mcp",
      "enabled": true
    }
  ]
}
```

Each enabled URL must use HTTP or HTTPS. Unreachable or invalid MCP servers are skipped so the agent can continue with its built-in tools.

## Run

```bash
dotnet run --project Xians.Examples/PromptDefinedAgent
```
