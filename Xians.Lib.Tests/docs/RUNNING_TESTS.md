# Running tests

From the Lib repo (`XiansAi.Lib/` or `XiansAi.Lib/Xians.Lib.Tests/`):

```bash
dotnet test
```

That is the default contributor loop: **unit tests + mock integration**. It does **not** hit a live Xians Server.

Live agent-against-Server coverage lives in **XiansAi.Server** (`dotnet test` there includes Lib-backed cycles). Lib’s `Category=RealServer` tests are an optional extra against a Server you own.

## What `dotnet test` includes

The test project sets a default VSTest filter of `Category!=RealServer`. A plain `dotnet test` therefore skips every test tagged `[Trait("Category", "RealServer")]`.

| Suite | Trait | Default `dotnet test` | Connects to a hosted Server? |
| --- | --- | --- | --- |
| Unit | (none) | Yes | No |
| Mock integration | `Category=Integration` | Yes | No (WireMock; Temporal only if `RUN_INTEGRATION_TESTS=true`) |
| RealServer | `Category=RealServer` | **No** | Yes — `SERVER_URL` / `API_KEY` from `.env` |

Passing Lib unit + mock tests does **not** prove a hosted Server is healthy. For that, run Server’s Lib-backed cycles, or opt into RealServer as below.

## Commands

```bash
# Default loop (unit + mock integration). Same as a filter of Category!=RealServer.
dotnet test

# Unit only
dotnet test --filter "Category!=Integration&Category!=RealServer"

# WireMock / in-process integration (not a hosted Server)
dotnet test --filter "Category=Integration"

# Opt-in: live Server from .env (not part of the default loop)
dotnet test --filter "Category=RealServer"

# Everything, including RealServer
dotnet test --filter "Category!=RealServer|Category=RealServer"
```

Any `--filter` you pass **replaces** the default exclusion. `dotnet test --filter "FullyQualifiedName~Knowledge"` can therefore pick up `RealServerKnowledgeTests` as well as unit/mock tests. Add `&Category!=RealServer` if you want to stay off the live Server.

## RealServer tests (optional)

**Where:** `IntegrationTests/RealServer/`

**When:** You have a Server you own (local or dedicated) and want a Lib-side smoke against it. Not required for a Lib PR; Server Lib-backed tests are the platform path.

**Setup:** copy `env.template` to `.env` (gitignored). `API_KEY` is a Base64-encoded client certificate, not a password. Do not commit keys. Do not point `SERVER_URL` at production unless that is an explicit, isolated check.

```bash
cp env.template .env
# Set SERVER_URL and API_KEY
dotnet test --filter "Category=RealServer"
```

Tests still no-op if credentials are missing (`RunRealServerTests` is false). The default filter means they are not even discovered on a plain `dotnet test`.

## Related

- Test categories in more detail: [TEST_TYPES.md](TEST_TYPES.md)
- Certificate / `.env`: [AUTHENTICATION.md](AUTHENTICATION.md)
- Platform contributor loop (Server, Lib, Studio): [Running tests after a change](https://xiansaiplatform.github.io/XiansAi.Docs/contribution/running-tests/)
