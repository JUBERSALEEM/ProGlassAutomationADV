# Agent intelligence + demo upgrade — audit and what shipped

Verified by reading this repo. There is no `dotnet` SDK in the sandbox, so Windows `dotnet build` remains the authority.

## Audit (what already existed)

| Area | Finding |
|---|---|
| AI | Gemini / Groq / Cohere via `IAiService`. Agent default is **local** (`LocalOnly` true). Cloud only with consent + key. |
| Agent | Isolated under `Agent/`. Deterministic `IntentDetector` → `AgentPlanner` → gated tools. No free SQL. |
| Tools | 13 read + navigate + one write (`update_sheet_stock`, confirmation required). No delete tool. |
| Whisper | `WhisperSpeechService` + `WhisperModelManager`. Agent uses `AgentWhisperBridge`. |
| TTS | Windows `SpeechSynthesizer` (`TextToSpeechService`). Speaks live; no file export API. |
| Memory | Session cap 50 (RAM). Approved facts on `agent.db`. |
| Context | Opt-in `IAgentContextAware` document ids only. `AgentReferenceResolver` for "it". |
| Screenshot | `ScreenshotService` — WPF render, FullHD–8K, full-page stitch. Save dialog only. |
| Recording / FFmpeg / UI Automation | **None.** No screen recorder, no ffmpeg package, no UIA driver. |
| Sample data | `SampleDataService` seeds only an **empty** DB; refuses when records exist. `ClearAllData` is Settings-only and confirmed. |
| Stock maths | Sheet Store: `Balance = Stock − Used`. Available SQM = `SqmPerSheet × (Stock − Used)`. No wastage ledger on sheets. `LastPurchaseDate` exists. |
| Modules | Dashboard, Sheet Store, PI, JO, production placeholders, DN, TI, Sales Report, Job Profitability, Company/User Center, Settings. |

## What this upgrade added (extension, not a rewrite)

- `Agent/Nlu/*` — normalize, expand, 1-edit unique typo fix, entity extract, follow-up slots.
- `get_stock_detail` — purchased = Stock, consumed = Used, available = Stock−Used. Wastage questions are answered honestly: no sheet wastage ledger.
- `create_demo_package` + `DemoDirector` — navigates via existing `AgentHost.Navigate`, captures via `ScreenshotService`, writes frames + SRT/VTT + HTML player + log. **MP4 only if ffmpeg is actually on PATH.**
- Demo isolation — never calls `ClearAllData`. Seed only when `HasExistingDataAsync` is false.
- Settings → **Demo Center** page.
- Tests: `NluRoutingTests` + reachability phrases for the new tools.

## Explicitly not claimed

- Continuous 1080p/4K movie recording with cursor zoom.
- Hindi/Arabic neural TTS files (Windows voice pack only).
- Autonomous clicking of every PI wizard field (no UIA driver).
- Recalculated stock formulas that disagree with Sheet Store.

## Files not modified

ERP document views, DN/TI/PI business rules, PDF composers, dashboard XAML, repositories, migrations.
