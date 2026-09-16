# Briefing Agent

A conversational check-in agent for **Agent Studio**, **Slack**, and **Microsoft Teams**. It answers questions in chat, then pings the same people on a schedule.

Markdown is rendered **only through the system prompt**. The LLM reply is sent as-is. There is no converter.

This is not notify-only and not a digest/job-done agent. Chat first, then interval check-ins.

## Features

- **Agent Studio chat** — Supervisor workflow handles inbound messages
- **Slack and Teams** — same worker; bots are platform integrations. Proactive sends replay `Scope`, `ThreadId`, `Authorization`, and `Origin`
- **Scheduled check-ins** — Temporal interval (default 10 minutes), not webhook or cron
- **LLM answers** — Anthropic (`claude-sonnet-4-6`) when `ANTHROPIC_API_KEY` is set
- **Reusable markdown prompt** — `MarkdownFormattingPrompt` selects Studio / Slack / Teams dialect from `scope`

## Prerequisites

1. **.NET 10.0** or later (`dotnet --version`)
2. A running **Xians server** (default `http://localhost:5001`) and **Temporal** (configured by the server)
3. **Agent Studio** at `http://localhost:3001`
4. An **API key** from Agent Studio → Developer / API keys. This is a Base64-encoded X.509 certificate (starts like `MIIE…`), not a random string. It must contain `O=` (tenant id) and `CN=` (user id)
5. An **Anthropic API key** for live answers. Without it, chat still connects and check-ins still send, but replies ask you to set the key

Slack and Teams bots are configured on the Xians server, not in this process.

## Setup

```bash
cd Xians.Examples/BriefingAgent
cp env.template .env
```

Edit `.env`:

```env
XIANS_SERVER_URL=http://localhost:5001
XIANS_API_KEY=                          # paste the Studio certificate
ANTHROPIC_API_KEY=                      # required for chat replies
ANTHROPIC_MODEL=claude-sonnet-4-6
PROACTIVE_CHECKIN_EVERY_MINUTES=10
```

`.env` is gitignored. Do not set `SERVER_URL` / `API_KEY` (those are lower-level names). This sample uses `XIANS_*`.

## Run

```bash
cd Xians.Examples/BriefingAgent
dotnet run
```

You should see:

```
Briefing Agent is running and connected to Agent Studio.
Open Agent Studio: http://localhost:3001
```

Then:

1. In Studio, activate **Briefing Agent**
2. Send one chat message (that registers you as a subscriber)
3. Wait `PROACTIVE_CHECKIN_EVERY_MINUTES` for a check-in
4. Chat from Slack or Teams the same way — the first message stores channel routing so later check-ins land in that thread

Do not expect a check-in immediately after your first message. The first workflow start only creates the schedule (`sendMessage: false`).

## Add markdown formatting to another agent

Copy **one file**: `MarkdownFormattingPrompt.cs`.

Append it to the LLM instructions. Detect the channel from the inbound `scope`, then send the model output unchanged:

```csharp
var instructions = MarkdownFormattingPrompt.Combine(
    "You are my agent's personality and task instructions.",
    context.Message.Scope);

// ChatOptions.Instructions = instructions

var reply = await llm.RunAsync(context);
await context.ReplyAsync(reply);   // no converter
```

`Combine` keeps your personality first, then adds shared "never HTML / protect code / no post-processor" rules plus the Studio, Slack, or Teams dialect.

| Surface | `scope` | Prompt tells the model to emit |
|---|---|---|
| Agent Studio | null / anything else | GFM (`**bold**`, `*italic*`, `~~strike~~`, `[text](url)`, `# Title`) |
| Slack | contains `Slack` (for example `Slack`, `app:slack:T123`) | `**bold**` `_italic_` `~strike~` `<url\|text>`; headings as sans-serif bold letters; numbered lists as `(1) item`; code as monospace letters; quotes as `▎ text`; tables as GFM with trailing pipes |
| Teams | contains `Teams` or `msteams` | GFM bold/italic/links; strikethrough as combining U+0336; headings as `**Title**` with a blank line between them; tables as GFM with **no** trailing pipe |

Do not send HTML. Do not add a markdown converter — leftover GFM on Slack/Teams will render badly because nothing rewrites it after the model.

## Proactive check-ins

After the first inbound chat:

1. The handler stores `participantId` plus Slack/Teams routing (`Scope`, `ThreadId`, `Authorization`, `origin` from metadata when it starts with `app:`)
2. It starts `CheckInWorkflow` once with `sendMessage: false`
3. The workflow creates interval schedule `channel-check-in-10`
4. Every N minutes the same workflow runs with `sendMessage: true`, loads subscribers, skips anyone active in the last minute, and sends as the Supervisor

Static check-in text (no LLM):

> Good morning/afternoon/evening — just checking in. I'm here if you need anything. What's on your mind?

You can reply to a check-in; that is a normal chat turn and refreshes `LastSeenAt`.

Changing the interval on an existing activation requires a new schedule name (or delete the old schedule). `CreateIfNotExists` will not update an old spec.

## Project structure

```
Xians.Examples/BriefingAgent/
  Program.cs                     # Register, Supervisor handler, start check-in workflow
  BriefingLlm.cs                 # Anthropic, history, personality
  MarkdownFormattingPrompt.cs    # Reusable channel system prompt (copy this)
  CheckInWorkflow.cs             # Check-in workflow + activities
  env.template
  README.md
```

## How it fits together

```
User (Studio :3001 | Slack | Teams)
  → Supervisor Workflow
      OnUserChatMessage
        Documents.SaveAsync(subscriber)
        Workflows.StartAsync<CheckInWorkflow>(false)   # once
        LLM (MarkdownFormattingPrompt) → ReplyAsync    # as-is

Temporal Schedule "channel-check-in-10" every 10 min
  → CheckInWorkflow(true)
      Documents.QueryAsync(subscribers)
      MessageActivities.SendMessageAsync (as Supervisor)
```

The `[Workflow("Briefing Agent:Check-In Workflow")]` prefix must stay equal to `Register({ Name = "Briefing Agent" })`.

## Troubleshooting

- **Auth fails** — `XIANS_API_KEY` must be the Studio certificate, not a placeholder string
- **Chat says to set ANTHROPIC_API_KEY** — add it to `.env` and restart `dotnet run`
- **No Slack/Teams check-ins** — send one message from that channel first so `Origin` / `Scope` / `Authorization` are stored. Leave `Origin` null unless it starts with `app:`
- **No check-in right after chat** — first start is schedule-only by design
- **Interval did not change** — rename `ScheduleName` or delete the old Temporal schedule
- **Bold looks italic on Slack** — Slack treats `*text*` as italic. The prompt requires `**bold**`
- **Inline code, fences, or quotes vanish on Slack** — backticks and `>` are stripped. The prompt requires monospace letters and `▎` quotes
- **Headings look italic on Slack** — the prompt requires marker-free bold letters, not `*`/`**`. Teams titles need a blank line between them
- **Numbered lists look like bullets on Slack** — the prompt requires `(1) item` instead of `1. item`
- **Slack tables look like a broken box** — the prompt requires GFM with trailing pipes (`\| Col A \| Col B \|` plus `\| --- \| --- \|`). Teams uses GFM with no trailing pipe
- **Strikethrough missing on Teams** — the prompt requires combining U+0336, not `~~strike~~`
- **HTML in Slack** — do not send HTML
- **Another agent still renders badly** — confirm you copied `MarkdownFormattingPrompt.cs`, called `Combine` with the inbound `scope`, and sent the reply with no extra converter

## Docs

- [Agents / Supervisor](https://xiansaiplatform.github.io/XiansAi.Docs/concepts/agents/)
- [Activations](https://xiansaiplatform.github.io/XiansAi.Docs/concepts/activations/)
- [Proactive messaging](https://xiansaiplatform.github.io/XiansAi.Docs/concepts/messaging-proactive/)
- [Scheduling](https://xiansaiplatform.github.io/XiansAi.Docs/concepts/scheduling/)
- [Slack integration](https://xiansaiplatform.github.io/XiansAi.Docs/server/slack-integration/)
