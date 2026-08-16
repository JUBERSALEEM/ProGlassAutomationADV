# ProGlass Agent — Architecture

## What it is

A **deterministic, gated assistant** bolted onto the ProGlass ERP. It can
look things up and open screens. It cannot create, edit or delete a single
business record.

## Isolation

Everything lives under `Agent/`. The only edits outside that folder are:

| File | Edit |
|---|---|
| `ViewModels/Settings/SettingsViewModel.cs` | `IsAgentPage` flag + `"Agent"` accepted key |
| `Views/Settings/SettingsView.xaml` | one nav-rail button + one section host |
| `Views/Settings/Sections/SettingsAgentSection.xaml(.cs)` | the page itself |

No ERP module, repository, model or view-model was modified. Deleting the
`Agent/` folder and reverting those three files removes the agent entirely.

## Pipeline

```
AgentRequest
   │
   ├─ 1. SANITIZE     AgentSecurityService.SanitizeUntrusted
   ├─ 2. DETECT       IntentDetector.Detect (deterministic, no LLM)
   ├─ 3. PLAN         AgentPlanner  → AgentPlanStep
   └─ 4. EXECUTE
         ├─ help / teach / approve  → local flows
         ├─ navigate → routed through the tool gate
         └─ tool
              ├─ registered?         unknown → refuse
              ├─ permission          identity + role
              ├─ confirmation        Write/Destructive always
              └─ execute
   ▼
AgentResponse { Success, Message, SpeakText, NavigateTarget }
```

Every stage is wrapped. Every `catch` produces a refusal — never a
best-effort action.

## Components

| Folder | Responsibility |
|---|---|
| `Core/` | `ProGlassAgent` orchestrator, request/response models, `AgentHost` composition root, `AgentAuditService` |
| `Interfaces/` | Every collaborator contract — the core has no concrete dependency |
| `Intent/` | Deterministic phrase → intent DSL |
| `Planning/` | Intent DSL → ordered steps |
| `Tools/` | The **only** way the agent touches the app |
| `Context/` | What is on screen, answer provenance, "it" resolution |
| `Permissions/` | Identity + role gate, confirmation policy |
| `Security/` | Split by concern: `DataBoundary/` `Consent/` `Redaction/` `SecretProtection/` `Network/` `Integrity/` |
| `Knowledge/` | Curated offline facts + seed |
| `Teaching/` | Trigger → action rules, inert until approved |
| `Memory/` | Session conversation (RAM) + approved facts (disk) |
| `Providers/` | Local (default) and gated cloud adapters |
| `Voice/` | Whisper readiness bridge, TTS adapter, listen→handle→speak pipeline |
| `Storage/` | `agent.db` — the agent's own database |
| `Configuration/` | `agent-settings.json` privacy posture |
| `Rules/` | Hardware probe + local-model advice (never downloads) |
| `UI/` | Settings page view-model |

## Data locations

```
%AppData%/ProGlassAutomation/Agent/agent.db             teaching · memory · knowledge
%AppData%/ProGlassAutomation/Agent/agent-settings.json  privacy posture
Windows DPAPI (per-user)                                provider API keys
```

The agent **never** opens `proglass.db`, `audit.db` or any other ERP
database directly. It reads business data only through the existing
repositories, via tools.

## The context layer, and what it does NOT collect

`Context/` lets the agent answer "how much is still pending?" about the
document already on screen. Its boundaries are deliberate:

| Observed | Not observed |
|---|---|
| Active module name | Screenshots, OCR |
| A short label a module *opts in* to publishing (`editing PI-2026-014`) | Keystrokes, field contents |
| In-session trail, capped at 10 entries | Microphone, continuous capture |
| | Customer names, prices, balances |

**Nothing in `Context/` is written to disk.** It is RAM only and dies with
the process. Background observation that silently accumulates a permanent
file is surveillance, not assistance.

There is no timer and no navigation hook — the trail updates only when the
agent is actually used, so nothing is watched while it sits idle. Clearing
the conversation also clears the trail.

Modules opt in via `IAgentContextAware`, a single string property, so a
module that does not implement it reports nothing and no property-name
guessing is involved. Five modules publish today: Proforma, Sheet Store,
Job Order, Delivery Note, Tax Invoice — each an identifier only, never a
value. A context label has no permission gate, which is exactly why no
priced or named data may travel through it.

## Lazy startup

`AgentHost.Ensure()` builds nothing until something asks. Opening the app
does not create `agent.db`, does not seed knowledge and does not construct
a single agent service. Startup cost is zero until the user visits
Settings → AI & Agent.
