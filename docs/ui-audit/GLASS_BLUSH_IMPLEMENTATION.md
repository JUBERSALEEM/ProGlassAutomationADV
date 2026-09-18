# GLASS BLUSH — 100% Visual Fidelity Implementation (Personal UI)

**Baseline:** `5d86f11` (continues the MASTER UI MODERNIZATION stream from
`5de4bc6`) · **Started:** 2026-09-18
**Visual source of truth:** the 10 reference PNGs in the delivery folder
(`glass-01-dashboard-overview.png` … `glass-10-ai-assistant.png`, all
**1408×768**, 16:9). They are authoritative for composition, spacing, color,
glass, glow, radius and typography. This document tracks every phase, every
screen, deviations, and verification status.

**Honesty rules in force:** STATIC VERIFIED ≠ RUNTIME VERIFIED ·
VISUALLY MATCHED ≠ NOT YET VERIFIED (no runtime exists in this environment —
no .NET SDK, no screenshots possible; GB-P16 is tooling-ready but NOT RUN).

---

## GB-P1 — MEASUREMENTS OF THE 10 REFERENCES (2026-09-18)

Method: ImageMagick analysis (no PIL available): 14-color quantized palettes
per reference, 64×36 luminance grids for region detection, 1×1 full-resolution
probes for exact colors, 24×24 corner probes for radius. Machine-readable
raw data captured in `/tmp/glass_blush_measurements.json` (working session
only).

### Canvas (all 10)
- **1408 × 768 px (16:9)**, assumed 1:1 DIP for a ~1440×810 window at 100%.
- Sidebar: content starts at x≈110 (most screens) / 176 (glass-01) →
  **sidebar ≈ 140px** (glass-01 rail ≈ 176 with wider padding).
- Page margin: bright regions end at x≈1320–1364 → **right margin ≈ 44px**;
  uniform **page padding 44px**.
- Header band: y 0–~110; KPI row y≈154–264 → **top bar ≈ 72px + page header**.
- glass-01 KPI row: 4 cards spanning 176–1320 → **KPI card ≈ 270×110, gap 28**.

### Color (measured, full-res probes)
| Role | Measured | Token value |
|---|---|---|
| Background bottom-left | `srgb(3,23,74)` / `srgb(2,21,67)` | `#021749` → `#021543` |
| Background mid | `srgb(2,68,142)` | `#02448E` |
| Background top-right glow | `srgb(34,133,183)` | `#2285B7` (azure glow corner) |
| Sidebar rail | `srgb(1,24,71)` | `#011847` @ 55% over bg |
| Card glass fill | `srgb(96,139,172)` over bg → ≈28% white | `#48FFFFFF` (28%) |
| Card glass (strong) | brighter KPI cards ≈35% white | `#59FFFFFF` |
| Card border | bright translucent white | `#73FFFFFF` (45%) |
| Sheen top edge | diagonal white highlight | `#99FFFFFF`→`#2EFFFFFF` @135° |
| Text primary | `srgb(253,255,255)` | `#F5FAFF` |
| Text muted | ice blue (palette family) | `#A9C3E6` |
| Accent cyan | `#2285B7` / `#2E8EB4` / `#2188B8` family | `#2FA8D8` |
| Accent glow | bright cyan highlights | `#57C7F0` |
| Success / Warning / Danger | mint / amber / coral pills | `#34D399` / `#FBBF24` / `#F87171` |
| **Night (glass-04 only)** corners `srgb(7,12,50)`, tiles `#152B50`, accent `#1891AA`/`#4B5A71` | midnight `#070C32`, navy glass `#152B50`, neon cyan `#4FD1E8` | `GlassBlushNight.*` |

Gradient direction: **dark sapphire bottom-left → azure glow top-right**
(`StartPoint 0,1 → EndPoint 1,0`), plus a soft cyan radial orb in the
top-right and a faint orb bottom-left (measured corner luminance spread).

### Geometry (measured)
- Card corner radius: corner probes show full card coverage from ~20px into
  the diagonal; AI-rendered edges ≈ 20–24px → **CardRadius 22**.
- Small radii (chips/badges) ≈ 12–16 → **SmallRadius 14** · pills fully
  rounded → **999**.
- KPI card 270×110 · card gap 28 → **24** (normalized) · card padding ≈
  20 · control height ≈ 44 (measured button/input bands) · stepper diameter
  ≈ 36.

### Effects (visual read, to be dialed at GB-P16)
- Frost: translucent white over a pre-blurred atmospheric background
  (WPF has no true backdrop blur on arbitrary surfaces → closest stable
  architecture = gradient + soft radial orbs behind translucent cards;
  recorded as a documented technique, §12 of the master prompt).
- Glow: cyan DropShadow (radius ≈ 18, opacity ≈ 0.55) on accent elements;
  never on table rows (performance, §23).
- Sheen: 135° white gradient top-edge on cards.

### Typography (visual read)
- Sans (Segoe UI family per app font). KPI numerals ≈ 40px Bold tabular;
  page titles ≈ 26 SemiBold; headings 20; body 15; captions 13; labels 12.
  → `GlassBlush.Font.*` scale in the token plane.

### Per-reference composition map
| Ref | Screen → existing WPF view | Composition (measured regions) |
|---|---|---|
| glass-01 | Dashboard → `OverviewDashboardView` | rail 140 · 4 KPI cards · wide area chart + donut · rounded invoice table |
| glass-02 | PI → `ProformaInvoiceView` + steps | header + pill buttons · 9-step glow strip · form card (left) + line-items card + total card |
| glass-03 | JO → `JobOrderDetailView` | header pills · 4-stage glow timeline · schedule card (left) + details card (right) |
| glass-04 | Production → `ProductionControlCenterView` | NIGHT palette · 3 big readouts · 6 work-center tiles · queue table |
| glass-05 | Sheet store → `SheetStoreView` | header + search pill + round add · 3 stat chips · masonry sheet cards with stock bars |
| glass-06 | Delivery → `DeliveryNoteListView`/`DeliveryNoteDetailView` | glass list (left) + detail card: driver block, route timeline, items table |
| glass-07 | Aging → `PiDashboardView` aging / `SheetProfitabilityView` | filter pills (Overdue active) · 3 stat cards · invoice table + glowing aging bars |
| glass-08 | Analytics → `ExecutiveReportView`/`SalesReportView` | filter bar · multi-line hero chart · radial gauge · bar cards |
| glass-09 | Settings → `SettingsThemesSection` | section list rail · theme tiles · accent swatches · density segmented control · motion toggle |
| glass-10 | AI → `AiChatView` | chat panel: glass bubbles (user solid / assistant glass) + embedded mini-table · suggestion chip rail |

---

## Per-screen records

### glass-01 — Dashboard Overview (GB-P4)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED (no runtime capture possible).
- **Architecture:** Personal view fork `Views/Personal/PersonalDashboardView.xaml` (+ minimal code-behind, no logic) referencing the **same `OverviewDashboardViewModel`**. Routed from the Personal shell via a scoped `DataTemplate` in `GlassShell.xaml` resources — Professional's app-level templates and view are untouched.
- **Components used:** GlassKpiCard (4 floating KPI cards), ChartCard + TrendChartControl (revenue bar), ChartCard + DonutChartControl (delivery mix), GlassCard + InsetCard pipeline stages (WorkflowStages), GlassCard + GlassDataGrid (RecentActivity, virtualized), GlassButton/GlassButtonSoft (module actions), StatusDot + live pulse, Skeleton (4-state contract: loading/error/empty/data preserved).
- **Contract preserved:** all bindings (SalesTiles/ProductionTiles/DeliveryTiles/InventoryTiles, MonthlySalesBars, DeliveryMixSlices, WorkflowStages, RecentActivity, LastRefreshed, IsLoading/HasError/ErrorMessage/HasRecentActivity, InProductionDisplay…), commands (NewInvoiceCommand, ViewJobsCommand, ViewDeliveriesCommand), chart control properties, i18n keys (Dashboard.*, Common.*).
- **KPI selection (measured 4 headline cards):** SalesTiles[0] Total Sales · ProductionTiles[0] In Production · DeliveryTiles[1] Dispatched · InventoryTiles[0] Stock Value.
- **Deviations (documented):**
  1. Per-screen top-bar actions (Add Invoice etc. in the reference top bar) rendered as a slim action row under the shared glass top bar — the shared shell keeps generic chrome; screen-specific actions live in the content. No functionality lost.
  2. KPI sparklines from the reference not rendered — the VM exposes no per-KPI spark series (PulseSpark is pulse-only); no data fabrication.
  3. Reference "invoice table" rendered as the RecentActivity glass table (the same data set the Professional view shows in its recent-documents card, table-form per the reference).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime screenshot fidelity: NOT YET VERIFIED (GB-P16 tooling).

### glass-02 — Proforma Invoice (GB-P5)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** Personal view fork `Views/Personal/PersonalProformaInvoiceView.xaml` on the **same `ProformaInvoiceViewModel`** (routed: `case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM` — verified; scoped DataTemplate in GlassShell).
- **Components used:** GlassCard header (back + actions), **9-step glowing stepper strip** (GlassBlush wizard step: completed = soft-success + check, active = accent-deep + cyan glow — same `IsCompleted`/`IsActive` data logic as Professional), glass step panel (same 9 step views, same `CurrentStepNumber` triggers), glass footer with `GlassBlush.Progress` (ProgressPercent), GlassButton/GlassButtonSoft.
- **Contract preserved:** IsListView/IsWizardView modes, NewFromInquiryCommand/NewInvoiceCommand/BackToListCommand/GoToStepCommand/PreviousStepCommand/NextStepCommand, Step1VM…Step9VM, CurrentStepNumber, ProgressPercent, StepProgressText, WizardSteps (StationCode/RailLabel/Subtitle/IsCompleted/IsActive), ALL AutomationIds (NewFromInquiryButton, NewInvoiceButton, BackButton, ContinueButton), i18n (ProformaInvoice.*, Action.Back).
- **Deviations (documented):**
  1. The nine step panels host the SAME Professional step views inside the glass step panel — the business form content keeps its existing form styling (forking all 9 step forms would duplicate the module's business UI; the glass composition = shell/strip/panel/progress). This is the "shared components express the mockup" path for the step content.
  2. Saved-invoices list (list mode) hosts the SAME `SavedInvoicesListView` inside a glass card (same reason).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-03 — Job Order (GB-P6)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** Personal view fork `Views/Personal/PersonalJobOrderView.xaml` on the **same `JobOrderViewModel`** (routed: `case "JobOrder": CurrentViewModel = JobOrderVM` — verified). Three glass tab pills (Workspace/God View/Activity) with the same `SwitchTabCommand` + `IsWorkspaceTab/IsGodViewTab/IsActivityTab` visibility pattern (two-button pattern preserved).
- **Components used:** GlassButton/GlassButtonSoft tab pills, GlassCard list host (SavedJobOrdersListView), **glass status timeline** (real `StatusOptions` + `SelectedStatus` from JobOrderDetailViewModel; active stage = cyan glow via the same StringEqualsMultiConverter mechanism), GlassCard detail host (the same `JobOrderDetailView` unified page), GlassCard god-view + activity hosts.
- **Contract preserved:** SwitchTabCommand (Workspace/GodView/Activity params), IsListView/IsDetailView, ListVM/DetailVM/GodViewVM/ActivityVM, all detail commands (BackToList, CreateDeliveryNote, OpenMasterDn, ExportPdf/Excel/Csv, Share, Save), JobOrderId/SourceProformaId/ConvertedDisplay/GrandTotalDisplay/StatusLabel/StatusBrushKey/StatusOptions/SelectedStatus, IsLoading/HasError/ErrorMessage, i18n (Common.Workspace/Activity, DeliveryNote.*, JobOrder.*). SwitchDetailTabCommand stays on the VM unrendered (Demo Hub dependency — same as Professional).
- **Deviations (documented):**
  1. The reference 4-stage glow timeline is rendered from the VM's `StatusOptions` list with the active stage glowing; per-stage "completed" state is not exposed by the VM (no stage index), so non-active stages render as pending (no state fabrication).
  2. List/god-view/activity/detail host the SAME Professional sub-views inside glass cards (documented hybrid — business content untouched; glass composition = tab strip, timeline, cards).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-04 — Production Control Center (GB-P7)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** Personal view fork `Views/Personal/PersonalProductionControlCenterView.xaml` on the **same `ProductionControlCenterViewModel`** (routed: `case "ProductionControlCenter": CurrentViewModel = ProductionControlCenterVM` — verified). **NIGHT palette**: the measured `GlassBlushNight.*` token set (midnight background `#070C32`, navy glass `#152B50` @25%, neon cyan `#4FD1E8`) applied via **view-scoped `GbNight*` styles** (BasedOn the shared glass components) — no shared-token changes, Professional untouched.
- **Components used (night variants):** GbNightCard (header/readouts/tables/detail/analytics), GbNightButton/GbNightButtonSoft (Load/Cancel + 8 stage commands), GbNightTab (3-pillar stage ribbon: Cutting/Tempering/DGU, same `Is*Tab` OneWay + `Select*TabCommand`), GbNightTextBox/GbNightComboBox (filter row), GbNightDataGrid + GbNightGridHeader/Cell/Row (virtualized master queue 13 cols, 3 mini queues, 6 analytics tables), GlassBlush.Progress (stage completion).
- **Contract preserved:** ALL metrics (TotalScheduled/CompletedDone/BalancePending Pcs+Sqm, StageCompletionPct), filters (Search/Readiness/Priority/DueRisk options + selected), all 9 stage commands (Start/Complete/Pause/Resume/Hold/ReleaseHold/Assign/SetPriority/Load/Cancel), Queue SelectedItem TwoWay, Executable/Blocked/Qc/Rework queues, JobProgress/ProcessProgress/QueuePressure/WorkstationLoad/Workstations, selected-item labels (Detail/Traceability/Material/Qc), HasProgress/ProgressLabel, Title/Subtitle.
- **Deviations (documented):**
  1. Reference "work center tiles" rendered as the 3 glowing readout cards + stage ribbon (the VM's tile data is the queue/workstation tables below — no tile model to invent).
  2. Night palette is screen-scoped (only this module is dark in the reference); the rest of the Personal shell stays light-sapphire per refs 01/02/03/05–10.
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-10 — AI Assistant (GB-P13)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** `Views/Personal/PersonalAiChatView.xaml` (+ code-behind = chat auto-scroll plumbing only) on the **same `AiChatViewModel`**. AI is an OVERLAY (not a module — mirrors Professional's `AiChatView` overlay in MainWindow). Hosted in GlassShell with the same `AiChatVM.IsOpen` visibility + `AiChatVM` DataContext; the Professional overlay is hidden when Personal is active (GB-P14).
- **Components used:** GlassBlush.ChatBubbleUser (accent, right) / ChatBubbleAssistant (glass, left + provider footer via Runs: ProviderIcon · AI.Via · ProviderName · TimeLabel), GlassBlush.SuggestionChip rail (Suggestions + SuggestionCommand), GlassBlush.TextBox (CurrentInput), GlassButton (SendCommand), TopIconButton (VoiceInputCommand/ClearChatCommand/ToggleChatCommand), glass empty state (AI.HelloIMProglassAI + hint).
- **Contract preserved:** Messages (ChatMessage immutable model), Suggestions, CurrentInput, HasMessages, ActiveProviderSubtitle, all 5 commands, all AI.* i18n keys.
- **Deviations (documented):**
  1. Error-state bubble (IsError) renders as the assistant glass bubble (the Professional view has a dedicated red bubble; a GbChatBubbleError variant is a candidate for the GB-P15 consistency pass — no data difference).
  2. Auto-scroll uses a CollectionChanged → ScrollToEnd in view code-behind (chat UX plumbing; no business logic).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

## GB-P14 — Professional ↔ Personal experience switch (the enabler)
- **Status:** ✅ STATIC VERIFIED · BUILD NOT RUN (no .NET SDK) · RUNTIME NOT VERIFIED.
- **Architecture (live, persisted, no restart):**
  - `AppearanceSettings.UiExperience` ("Professional" default | "Personal") — additive config field; old `config.json` hydrates to the default via `ConfigService.Load`'s null-coalescing pass. Persisted via `ConfigService.SaveDebounced` on every switch (same pattern as the Appearance settings).
  - New partial `ViewModels/MainViewModel.Experience.cs`:
    - `IsPersonalExperience` / `IsProfessionalExperience` — loaded once via a null-safe field initializer (settled before the ctor's first `CurrentViewModel` assignment).
    - `ProfessionalContent` / `PersonalContent` — the two experience-scoped module-host bindings. **Exactly one is non-null at a time ⇒ exactly ONE module view instance exists** (no double instantiation / double data loads).
    - `UpdateExperienceContent()` — called from the `CurrentViewModel` setter (ONE added line in `MainViewModel.Part1.cs`, additive, documented in-line) so navigation follows the active experience.
    - `ToggleExperienceCommand` — lazy, no ctor edit.
  - `MainWindow.xaml`: GlassShell added (RowSpan 2, ZIndex 30, visible ⇔ `IsPersonalExperience`); Professional top bar, content card (now bound to `ProfessionalContent`) and drawer/dim overlay collapse in Personal mode; Professional AI overlay hidden in Personal mode (Personal shell hosts `PersonalAiChatView` on the same `AiChatVM`); read-only banner + license overlay + agent chat remain in both modes (safety surfaces).
  - `GlassShell.xaml`: content host now bound to `PersonalContent`; mirror toggle button in the glass top bar (`ToggleExperienceCommand`).
  - Toggle affordances: Professional top bar `Shell.ExperienceSwitch` (E771) + Personal top bar `Shell.ExperienceSwitchPersonal` (E731).
- **State semantics:** the module VM (`CurrentViewModel`) is untouched — switching experiences preserves module state (selected document, filters, loaded data); only the VIEW is recreated ("safe shell recreation"). Documented per the master prompt's fallback allowance.
- **Template resolution in the Personal shell:** forked modules (Dashboard, PI, JobOrder, ProductionControlCenter, AI) resolve to their Personal glass views via `GlassShell`-scoped DataTemplates; **un-forked modules fall back to their Professional views** (template lookup walks up to `MainWindow.Window.Resources`) — functional, documented hybrid; Personal coverage grows as GB-P8…P12 land.
- **Known limitation (→ GB-P15):** the MainWindow-level popups (user-switch / notifications / settings quick panel) are anchored to the Professional top-bar buttons; in Personal mode they still open but anchor to window origin. Personal-side popup anchors are a GB-P15 item.
- **Verification:** XML well-formed (MainWindow + GlassShell); brace/paren balance on all touched C# (delta vs HEAD clean); xaml_validator 0 ERROR. BUILD/TESTS NOT RUN, RUNTIME NOT VERIFIED.

## Phase status

| Phase | Status | Commit |
|---|---|---|
| GB-P1 tokens + visual foundation | ✅ STATIC VERIFIED (tokens authored from measurements; runtime NOT VERIFIED — no .NET SDK) | `GB-P1` |
| GB-P2 shell | ✅ STATIC VERIFIED (shell + styles; runtime NOT VERIFIED — no .NET SDK) | `GB-P2` |
| GB-P3 shared components (21/21) | ✅ STATIC VERIFIED (runtime NOT VERIFIED — no .NET SDK) | `GB-P3` |
| GB-P4 dashboard (glass-01) | ✅ STATIC VERIFIED (PersonalDashboardView + shell routing; runtime NOT VERIFIED) | `GB-P4` |
| GB-P5 proforma invoice (glass-02) | ✅ STATIC VERIFIED (PersonalProformaInvoiceView + 9-step glow strip; runtime NOT VERIFIED) | `GB-P5` |
| GB-P6 job order (glass-03) | ✅ STATIC VERIFIED (PersonalJobOrderView + glass tab strip + status timeline; runtime NOT VERIFIED) | `GB-P6` |
| GB-P7 production control (glass-04) | ✅ STATIC VERIFIED (PersonalProductionControlCenterView, night palette; runtime NOT VERIFIED) | `GB-P7` |
| GB-P13 ai assistant (glass-10) | ✅ STATIC VERIFIED (PersonalAiChatView, glass bubbles + chip rail; runtime NOT VERIFIED) | `GB-P13` |
| GB-P14 experience switch (live, persisted) | ✅ STATIC VERIFIED (single-view-instance design, safe shell recreation; BUILD NOT RUN) | `GB-P14` |
| GB-P8…P12 screens (5) | pending | — |
| GB-P15 global visual consistency | pending (incl. Personal-side popup anchors + error-bubble variant) | — |
| GB-P16 runtime screenshot fidelity | **NOT RUN — no WPF runtime in this environment** (tooling to be delivered) | — |
| GB-P17 DPI/a11y/performance | pending (static parts only) | — |
