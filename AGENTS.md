# XiansAi.Lib

Instructions for every AI coding tool working in this repository. Claude Code reads this through `CLAUDE.md`,
Cursor and Copilot read it directly. Repo-specific rules sit above the workflow section; the workflow section
is managed by xians-dlc and is rewritten on `xians-dlc update`.

## What this repository is

`Xians.Lib` is the .NET 10 client library (NuGet package `Xians.Lib`) that agents use to talk to the Xians
platform: agent registration, Temporal workflows, messaging, knowledge, HITL tasks, scheduling, secrets and
multi-tenancy. `Xians.Lib.Tests` holds unit and integration tests, `Xians.Examples` holds runnable samples.
Issues for this library are tracked in the XiansAi.Server repository, so a branch here references a Server
issue number.

## Start here

- Library overview and quick start: `README.md`
- Feature guides: `Xians.Lib/docs/` (start with `GettingStarted.md`, then the guide for the area you touch)
- Test layout and categories: `Xians.Lib.Tests/README.md`

## Validation

- Build: `dotnet build XiansAi.Lib.sln`
- Unit tests: `dotnet test Xians.Lib.Tests --filter "Category!=Integration&Category!=RealServer"`
- Integration tests need a running server; do not run them by default and say so in `ship.md` when skipped.

## Rules for this library

- **Public surface is a contract.** Anything `public` in `Xians.Lib` ships to every agent on NuGet. A removed
  or changed public member is a breaking change and needs a major version bump; mark the old member
  `[Obsolete]` with a message first and remove it in a later release. Record every public change in the
  "Public contracts" row of the change plan.
- **Temporal determinism.** Workflow code (anything that runs inside a Temporal workflow) has no I/O, clock,
  random, environment or network access. I/O lives in activities, which are idempotent, carry timeouts and a
  deliberate retry policy, and thread the cancellation token into every external call. Command order changes
  consider in-flight workflows.
- **Tenancy.** Every call that reaches the server carries the tenant from the context; never hard-code or
  default a tenant. See `Xians.Lib/docs/Multi-tenancy.md` and `DeveloperGuide_TenantHandling.md`.
- **Async and cancellation.** Async methods end in `Async`, accept a `CancellationToken`, and never block on
  async work.
- **Logging.** Use the structured logger wrapper already in the touched area (`Xians.Lib/docs/LOGGER_WRAPPER_GUIDE.md`);
  never log secrets, API keys or message bodies.
- **Docs travel with the change.** A behaviour, setup or public contract change updates the matching guide in
  `Xians.Lib/docs/` and, when an example is affected, the example.
- **Keep changes scoped.** Search for an existing helper, model or extension before adding one, and do not
  reformat files you are not changing.

<!-- dlc:start (managed by xians-dlc; rewritten on `xians-dlc update`. Repo-specific rules go outside these markers.) -->
## Development workflow (xians-dlc)

This repository uses the gated xians-dlc workflow: every change is tracked against an issue and walks a signed
`spec.md`, `plan.md` and `ship.md` in `docs/work-item-log/<issue>/`. Git hooks on the developer's machine enforce it.

- **If `xians-dlc` is not available in this clone, run `npm install --prefix tools/dlc` first.** It installs the
  package and the git hooks; a fresh clone does not have them yet.
- **Before any work in this repository, run `xians-dlc guide` and follow it.** It is the full set of rules,
  served from the package so it stays current. Run it again in every new session; on a work-item branch, run
  `xians-dlc status` first and resume from the gate it names.
- When the developer names an issue and wants to start ("work on #456"), the guide tells you how to create the
  branch, run `xians-dlc init`, fill the artifacts and request each gate. Do not wait to be asked for each step.
- The developer approves gates; you never do. Never write `signoff.log`, never run `xians-dlc approve`, never
  commit or push outside the signed hand-off, and never bypass git hooks or signing.
- Writing rule for every artifact, log entry, commit message and PR text: plain, relaxed sentences with normal
  conjunctions and articles; no twin em-dashes, clipped semicolon fragments or meta-commentary about structure;
  readable by someone who has not opened the code.
- The command is `xians-dlc`, installed under `tools/dlc/`. If it is not on the PATH, use
  `npx --prefix tools/dlc xians-dlc ...`. When `xians-dlc status` says the workflow is switched off, this
  section does not apply.
<!-- dlc:end -->
