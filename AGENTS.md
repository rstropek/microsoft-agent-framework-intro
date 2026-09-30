# AGENTS.md

## What this is

A live-demo sample for a conference talk about the Microsoft Agent Framework in C#. It is a teaching aid, **not production
software**: no auth anywhere, in-memory state, hard-coded ports and data, sensitive telemetry data switched on, no tests.
Do not "harden" it; readability on a projector beats robustness.

Priorities when changing anything:

- Each numbered step under `src/` demonstrates one talk section. Its `Program.cs` must stay short and contain only that
  section's topic. Reusable or already-explained code belongs in `src/Lissie.Common`.
- Fewer lines and fewer concepts win. Do not add abstractions, options or error handling a demo does not need.
- The audience is experienced C# developers: no comments that explain C#, only short comments marking a talking point.
- The theme is part of the content. The chat user is Lissie, a cat; the agent is her staff. Karin is the Primary Human,
  Rainer the Secondary Human (and the presenter). Tool results are funny but deterministic. Keep that tone and keep model
  answers short (the persona instructions enforce 2-3 sentences; demos must stay fast).
- `storybook.md` is the presenter's cheat sheet. If you change a command, a prompt-relevant behavior, a port or a tool
  name, update it.

## Commands

```bash
dotnet build LissieAgents.slnx                              # must end with 0 warnings
dotnet format LissieAgents.slnx --verify-no-changes         # must be clean

scripts/start-backends.sh                                   # Aspire dashboard (Docker) + MCP server + both A2A agents
scripts/stop-backends.sh                                    # --all also removes the dashboard container

# Every console step reads prompts from stdin, so runs can be scripted ("y"/"n" answer approval prompts):
printf 'Dispense 30 g of kibble.\ny\n' | dotnet run --project src/02-Tools

# Same code on another model provider (env vars only, see gotchas):
LLM_PROVIDER=openai-compatible LLM_ENDPOINT=https://openrouter.ai/api/v1 LLM_MODEL=z-ai/glm-5.3-flash \
  dotnet run --project src/02-Tools

# Smoke tests without an agent
npx -y @mcpjam/cli --no-telemetry tools list --url http://localhost:5101/mcp
curl -s http://localhost:5201/.well-known/agent-card.json
curl -s http://localhost:18888/api/telemetry/traces         # what reached the dashboard
```

Documentation lookups: use the Microsoft Learn CLI (`npx -y @microsoft/learn-cli search|fetch|code-search`), see the
`microsoft-docs` skill.

## Gotchas

**Build and style**

- Warnings are errors, `AnalysisLevel=latest-recommended`, and code style is enforced in the build. Typical failures:
  missing collection expression, block-scoped namespace, `logger.LogInformation(...)` instead of `[LoggerMessage]`
  source generation (CA1848).
- .NET 10 / C# 14 only. C# 15 features are not available.
- Package versions are pinned centrally in `Directory.Packages.props`; several Agent Framework, A2A and AG-UI packages are
  previews. Microsoft Learn partly lags behind them: verify APIs against the restored assemblies in `~/.nuget/packages`
  or the samples in `microsoft/agent-framework` before relying on a docs snippet.
- Only two suppressions exist (`OPENAI001`, `CA1708`), each justified where it is declared. Add new ones the same way:
  per rule ID, with a reason.

**Configuration and secrets**

- Endpoint, deployment name and API keys live in user-secrets or environment variables, never in the repo. All projects
  share one `UserSecretsId`, so a secret set via `--project src/Lissie.Common` applies everywhere.
- Never store `LLM_PROVIDER`, `LLM_ENDPOINT` or `LLM_MODEL` in user-secrets: it would silently switch every project.
- Web projects read user-secrets only in the `Development` environment (the launch profiles set it).
- The Azure path authenticates with the Azure CLI login. The first model call of each process is noticeably slower.
- The deployment name appears at runtime in traces and in AG-UI `RUN_FINISHED` events. Keep it out of docs and commits.
- `src/01-HelloLissie` creates the Azure client explicitly on purpose; the provider switch applies from step 02 on.

**Agent Framework behavior that is easy to get wrong**

- `AIFunctionFactory.Create` on a local function in a top-level `Program.cs` needs `name:`; otherwise the tool gets a
  compiler-generated name.
- Agent-run middleware: first registered is outermost. Function-calling middleware: last registered runs first.
- An `IChatClient` middleware that answers without calling the model must pass `options?.ConversationId` through, or the
  agent throws when history is service-managed (Responses API).
- Tool approvals are bound to the `AgentSession` that raised them.
- Tools return a refusal string instead of throwing: exception details are hidden from the model in both hosts.
- A2A hosting: `.WithInMemorySessionStore()` defaults to isolation, which needs an auth-based key provider; without
  `withIsolation: false` every request fails with HTTP 500 and the cause is swallowed. The agent card URL must match the
  port in `launchSettings.json`.
- AG-UI hosting: `AddAIAgent(name, factory)` requires the agent's `Name` to equal the key. Multi-turn depends on the
  session store; without it each request starts a fresh session.
- `side/AgUiConsole` reads the raw SSE stream on purpose: `AGUI.Client` does not surface tool-call events.
- Workflows: an agent bound directly into a graph only runs when it receives a `TurnToken`, which is why agents behind the
  switch are wrapped in `SpecialistExecutor`.
- `Lissie.SmartHome` must keep referencing only `Microsoft.Extensions.AI.Abstractions`, and `side/SmartHomeMcp` must keep
  containing no tool logic: "one tool implementation, two hosts" is the point of step 05. String results arrive over MCP
  with an extra pair of JSON quotes; that is known and cosmetic.

**Processes**

- Ports are fixed (5101, 5201, 5202, 5300, dashboard 18888 / OTLP 4317) and referenced in code, scripts and docs.
- Never `pkill -f <project name>`; it can match your own shell. Stop by port: `lsof -ti :5101 | xargs -r kill`.
- Steps 05 (`-- mcp`), 07 and 08 need the backends running; they fail fast with a hint if not.

**Git**

- Do not add AI attribution lines (`Co-Authored-By`, "Generated with") to commits in this repository.
