# ProGlass Automation — Implementation Status

Generated 16 Aug 2026 · branch `arena/01a008cc-proglassautomationadv` · HEAD `f25bcdb`

Verified by reading the code in this repo, not from memory. Where something
could not be verified without a Windows build, it says so.

---

## 1. Settings — layout audit (this session)

### Fixed

| # | Where | Defect |
|---|-------|--------|
| 1 | `SettingsSectionStyles.xaml` · `PageSubtitleStyle` | No `TextWrapping`. Sibling `HintTextStyle` had it. The AI & Agent subtitle (71 chars, longest in the app) rendered as one unbroken line and pushed content past the right edge. Affects **11 usages** app-wide. |
| 2 | `SettingsAgentSection.xaml` | 4 rows of `[ * text │ Auto button ]` had no right gutter, so wrapped text ran flush into the button. `SettingsAiSection` already used `Margin="0,0,20,0"`; the Agent page did not follow it. |
| 3 | `SettingsAgentSection.xaml` | PROVIDER column was a hard **180px**. Fixed pixel columns cannot shrink, so on a narrow pane it pushed the API KEY box off the card. Now `Auto`/`MinWidth`. |
| 4 | `SettingsAgentSection.xaml` | 3 paired-button rows used `StackPanel`, which never reflows — the second button clipped. Now `WrapPanel`. |
| 5 | `SettingsAgentSection.xaml` | `agent.db` / `agent-settings.json` absolute `%AppData%` paths could stretch the card. Now trimmed, full value on hover. |
| 6 | `SettingsAppearanceSection.xaml` | Fixed 170px module-label column with no trimming clipped long names mid-glyph. The neighbouring filename column already trimmed. |

Items 2–5 are all in **Phase F code I wrote** — cases where the Agent page
did not follow conventions the older settings pages had already set.

### Checked and found correct

- **Page switching is sound.** All seven `Is*Page` flags derive from one
  `SettingsPage` string, are mutually exclusive, and the setter raises
  `OnPropertyChanged` for all seven. Pages cannot render simultaneously.
- All seven sections share the same correct shell (Auto header + star
  content inside a `ScrollViewer`).
- Taught-rules list already has an empty state bound to `HasRules`.
- No Grid-cell collisions anywhere in Settings.

---

## 2. ProGlass Agent (Phase F) — what actually works

**33 `.cs` files, 9 docs, isolated under `Agent/` + one Settings page.**
The "never modify existing ERP behavior" constraint was honoured.

### Working

- **Core** — `ProGlassAgent.HandleAsync`, fail-closed, audited.
- **Intent** — deterministic, no LLM. Precedence: help → teach → approve →
  tool → nav → approved teaching (user rules last, so they cannot shadow
  system behaviour).
- **Tools** — 8 read tools + `navigate`, capped results, truthful failures.
  Phase N added `get_profitability`, which reads the profit Step 9 already
  persisted rather than recalculating it.
- **Permissions** — identity must equal `AuditService.CurrentUser`;
  Read gated on `RequiredPermission`.
- **Teaching** — propose → approve → revoke, nothing acts until approved.
- **Memory / Knowledge** — session cap 50; 6 seeded glass facts;
  export/import; separated clears that never touch ERP data.
- **Security** — LocalOnly defaults **TRUE**, consent cleared when
  re-enabled; redaction; injection filtering; https-only whitelist.
- **Secrets** — DPAPI via `SecretVault`, prefix `agent.provider.`.
- **Providers** — local default; cloud only with `!LocalOnly && consent &&
  configured`; any error falls back to local.
- **UI** — Settings → AI & Agent (privacy, credentials, rules,
  memory/knowledge, status). `AgentHost.Ensure()` is lazy, so `agent.db`
  is not created until the page is opened.

### Not working / not built — the honest gaps

| Gap | Impact |
|-----|--------|
| ~~No chat UI~~ | **CLOSED (Phase L).** `Agent/UI/AgentChatView` + `AgentChatViewModel`, opened from the top-bar robot button. Teaching can now be proposed conversationally. |
| ~~`AgentHost.Navigate` never assigned~~ | **CLOSED (Phase K).** Wired to `MainViewModel.NavigateFromAgent`, which routes through the same switch the sidebar uses and marshals to the UI thread. |
| ~~`AgentHost.Confirm` never assigned~~ | **CLOSED (Phase K).** Wired to a Yes/No modal defaulting to No; fail-closed on any exception. |
| **No `Tests/Agent/**`.** | The spec's 8 required cases (offline, cloud-disabled, no-API-key, unauthorized-write, missing-confirmation, prompt-injection, corrupted-model, interrupted-download) are unwritten. **Still the largest remaining gap.** |
| **No Write/Destructive tools at all.** | Deliberate and safe, but the permission/confirmation machinery is therefore **untested in practice**. |
| **No local model runtime.** | `LocalReasoningProvider` answers from the knowledge base only. `LocalModelAdvisor` recommends but never downloads, as instructed. |

---

## 3. Pricing / P&L status

| Phase | State |
|-------|-------|
| A/B/C — Sheet Store pricing, Step 9 Profit & Loss, Job Profitability | Shipped |
| D — Audit with reason notes | Shipped |
| E — Global search | Shipped |
| G — Cost the sheet the user chose | Shipped `ce9077b` |
| **J — LAM Sheet Store parity** | Shipped `6814ed3` |

### Two bugs found and fixed while doing Phase J

1. **Laminated units were costed as single glazing.** Step 9 only knew DGU
   as two-pane, so every LAM spec dropped the **inner pane entirely** and
   priced production from SGU boxes the LAM panel never fills.
   Worked example (40 SQM, 6mm Clear both sides, 3210×2250 @ 46 AED/SQM,
   10% wastage, 12 AED/SQM PVB, 20 AED/SQM processing):

   | | Before | True |
   |---|---|---|
   | Outer sheets | 2,325.65 | 2,325.65 |
   | Inner sheets | — dropped | 2,325.65 |
   | Production | 0.00 | 1,280.00 |
   | **Total** | **2,325.65** | **5,931.30** |

   Understated by **3,605.65 AED**, biased toward inflating profit.

2. **`LoadSheetStoreAsync()` had no caller.** Present since Phase A on all
   three panels but never invoked — sheet-size dropdowns would have been
   **permanently empty** and every store price 0.00. Now called on Step 4
   construction and refreshed on step entry.

### Still open

- **DGU+LAM is a placeholder.** Its VM is empty by design, its view says
  COMING SOON, APPLY refuses to save. Deliberately left alone: the pricing
  rule (does the laminated pane replace outer or inner? does the
  ASP-inclusive rule still apply?) is a **business decision, not a coding
  one**.
- **The LAM production formula is my inference** — PVB material + cutting
  + tempering on both panes, per SQM. If lamination is charged per
  autoclave cycle or per pane, that branch needs changing.
- **Step 6 Other Charges** — out of scope per your instruction.

---

## 4. Layout fixes (commit `ed931e6`)

8 Grid faults, one root cause: a child missing `Grid.Row`/`Grid.Column`
defaults to cell (0,0) and draws **on top of** a sibling.

- 3 God Views (TaxInvoice/JobOrder/DeliveryNote) — REFRESH button over the title
- Ti + Dn OverviewSection — stat tiles overprinting each other
- Ti + Dn OverviewSection — **entire content card on top of entire stats card** (worst)
- VersionsDialog — empty message flashing over the list

Repo-wide now: **1 finding**, `MainWindow.xaml`'s Slider `Track` — correct by design.

---

## 5. Verification reality

**There is no dotnet SDK in this environment — nothing here has been
compiled.** A real `dotnet build` on Windows remains the authority.

Substitute harness (`/home/user/verify/`, outside the repo, lost on sandbox reset):

| Script | Result |
|--------|--------|
| `brace_scan.py` | all changed `.cs` balanced |
| `xaml_wellformed.py` | 121/121 |
| `binding_check.py` | Agent 24/24, LAM 21/21, Step 9 20/20 |
| `resource_check.py` | 0 unresolved |
| `overlap_scan.py` | 8 → 0 real faults |
| `text_overflow_scan.py` | 1 → 0 |

Because the fixes above are **visual**, please eyeball: Settings → AI &
Agent, Settings → Appearance, the three God Views, and Tax Invoice /
Delivery Note detail.

---

## 6. Recommended next steps, in priority order

1. **Build on Windows and eyeball the fixed screens.** Everything else is
   unverified until this happens.
2. **Agent chat UI** — the single highest-value gap. Without it the agent
   cannot be taught conversationally, which was your core requirement.
3. **Wire `AgentHost.Navigate`** to `MainViewModel.Navigate` — one line,
   makes the registered nav tool actually work.
4. **Confirm the LAM production formula**, and give me the DGU+LAM rule if
   you want it built.
5. **`Tests/Agent/**`** — the security cases matter most.
6. Commit the verify harness into the repo so it survives and you can run it.
