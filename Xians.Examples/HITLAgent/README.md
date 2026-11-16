# HITL Agent Example

A conversational agent that answers normally, and opens a human-in-the-loop task when you ask for one.

## What it does

- Chats through a small model agent on the supervisor workflow
- Exposes one tool, `CreateHitlTask`
- The model calls that tool only when you ask for a review, approval, or other human decision
- The tool starts `HitlTaskWorkflow`, which creates the task and messages you again with the decision

Chat handlers run as Temporal activities, and a HITL task can only be created from a workflow. The tool starts the workflow instead of creating the task itself.

## Configuration

Copy `.env.example` to `.env`:

```env
XIANS_SERVER_URL=http://localhost:5005
XIANS_API_KEY=your_api_key_here
OPENAI_API_KEY=your_openai_key_here
```

## Run

```bash
cd Xians.Examples/HITLAgent
dotnet run
```

## Try it

Ordinary chat stays a conversation:

```text
What can you help me with?
```

Asking for a decision calls the tool:

```text
Create a HITL task to approve the Q4 budget
Please ask someone to review the vendor contract and let them approve, reject, or hold it
```

The model fills in the title, description, draft, actions, and optional timeout. Actions default to `approve, reject`. Leave the timeout out and the task waits until someone completes it.
