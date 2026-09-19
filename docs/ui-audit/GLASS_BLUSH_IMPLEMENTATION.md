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
A first REAL build was run externally on 2026-09-19 and its errors were
fixed at the source (GB-B1), with a second fix round (GB-B2: TargetName in
Style triggers + a non-existent DropShadowEffect property) and a third
(GB-B3: a C# compile blocker — unparenthesized ternary in a string
interpolation — which was also the root of a ~100-error designer cascade)
and a fourth (GB-B4: a missing `System.Collections.Specialized` using in
the GB-P13 code-behind — root of the last 3 active CS errors; the 77 XDG
errors in that list were verified cascade, all 54 named types exist).
After GB-B3/B4 the build went GREEN (verified by the user's runtime: the
app now compiles, starts, opens the DB, signs in) — and the FIRST RUNTIME
crash followed (GB-B5: a star GridLength `"0.94*"` on a Length property in
GlassShell, which only fails at BAML runtime parse, not at markup
compile) — fixed with a ScaleLengthConverter) and a second (GB-B6: a
ContentPresenter-targeted ItemContainerStyle inherited via BasedOn was
applied to explicit ContentControl item containers — startup crash on
first layout; one style fixed, 32 sites cleared; validator +W23).
Runtime re-verification is still due in the SDK environment.

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
- **2026-09-19 template-dispatch fix (found during GB-P10):** the GlassShell template targeted `OverviewDashboardViewModel`, but the routed module VM is `DashboardViewModel` (which hosts `OverviewVM`/`TaxInvoiceVM` as tabs). WPF implicit DataTemplates match the **exact** type, so the Personal dashboard silently fell back to the Professional `DashboardView`. Fixed: template now targets `vm:DashboardViewModel`; `PersonalDashboardView` re-points `DataContext="{Binding OverviewVM}"` at its root (all bindings + the NewInvoice/ViewJobs/ViewDeliveries commands live on `OverviewDashboardViewModel` — verified). Lesson recorded: implicit-template dispatch is a RUNTIME behavior — static validation cannot prove a Personal template actually wins the lookup.
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
  1. (RESOLVED in GB-P15) Error-state bubble (IsError) initially rendered as the assistant glass bubble; GB-P15 added the dedicated `GlassBlush.ChatBubbleError` coral variant and switched the AI bubble to the model's `IsAiResponse` flag.
  2. Auto-scroll uses a CollectionChanged → ScrollToEnd in view code-behind (chat UX plumbing; no business logic).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-05 — Sheet Store (GB-P8)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** `Views/Personal/PersonalSheetStoreView.xaml` (InitializeComponent only) on the **same `SheetStoreViewModel`** (routed: `case "SheetStore"` → SheetStoreVM — verified). Light-sapphire palette (glass-05 is NOT the night reference; only glass-04 is night).
- **Components used:** GlassBlush.GlassCard (header + rows), GlassBlush.KpiCard ×3 (Total Sheets → `TotalStock`, Value → `TotalSellingValue` "AED {0:N0}", Low Stock → `LowStockCount` in Danger), GlassBlush.GlassButton (glowing + add), GlassBlush.TextBox (search → `SearchText`), GlassBlush.Pill (per-sheet stock count), GlassBlush.Progress (per-sheet stock bar, tinted by balance ratio), GlassBlush.EmptyState.
- **New UI-only helper:** `Converters/StockLevelBrushConverter.cs` (pure presentation mapping balance/stock → GlassBlush.Success ≥0.60 / Warning ≥0.30 / Danger below; MultiBinding {Balance, Stock}; fails soft). Declared view-locally — no shared-file change.
- **Reference fidelity:** card grid (5-up wrap, 252px cards) with vector glass-pane illustration (pure paths/gradients — no per-row effects), title = `{ThicknessMm}mm {Color}`, dimensions = `SizeDisplay`, quantity pill = `Stock`, stock bar = `Balance`/`Stock` (colored by ratio). Selected card = glowing accent border via `ReferenceEqualityConverter` MultiBinding vs `SelectedSheet` (Professional's exact selection mechanism).
- **Contract preserved:** `Sheets`/`Items` collections, `SearchText`, `AddCommand`, `SelectSheetCommand` (click, same command+parameter), `ShowGlassCommand`/`ShowConsumablesCommand` + `IsGlassView`/`IsConsumablesView` tab pair, `HasSheets`/`IsEmpty`/`HasItems` states, all 7 KPI sources (3 shown per reference; the remaining 4 stay in the Professional KPI strip).
- **Deviations (documented):**
  1. Professional row-list + 7-wide KPI strip + filters/sort combo row + right slide detail panel + add/edit inline workspace are Professional-mode surfaces not shown in the glass-05 reference; the Personal view renders the reference surface (card grid) only. `OpenEditPanelCommand` (double-click) is therefore not wired in the Personal view — add/edit + detail panel are reached in Professional mode. Selection/add/search/mode-toggle fully wired.
  2. Card grid uses a WrapPanel `ItemsControl` (cards, not a virtualizing DataGrid) — sheet lists are card-bounded in the reference; virtualized DataGrid remains in Professional mode (performance rule honored there).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-06 — Delivery Note (GB-P9)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Architecture:** `Views/Personal/PersonalDeliveryNoteView.xaml` (InitializeComponent only) on the **same `DeliveryNoteViewModel`** (routed: `case "DeliveryNote"` → DeliveryNoteVM — verified at Part2 L255). Two-column glass layout per the reference: left list card + right detail card.
- **Contract preserved (verified member-by-member):** `ListVM.AllItems` (flat DN rows, virtualized Recycling ListBox), per-row `DeliveryNoteId/CustomerName/ProjectName/DeliveryTime/StatusLabel/StatusBrushKey/IsSelected`, `ListVM.SelectRowCommand` (click, same command+parameter as Professional), `DetailVM` (`DeliveryNoteId/DriverName/VehicleNumber/DeliveryProgressPercent/VisibleItemRows`), item rows (`GlassRef/DimensionDisplay/OrderedQty/IsFullyDeliveredLine`).
- **Components used:** GlassBlush.GlassCard (both columns + driver card + items card), GlassBlush.Pill (time + plate), GlassBlush.Progress (delivery progress), GlassBlush.EmptyState (no deliveries / select-a-delivery), GlassBlush.SectionTitle (table headers), local `GbDnRow` (glass tile + `IsSelected` glow — same row-property mechanism Professional uses).
- **Deviations (documented):**
  1. Status pill = glass tint + colored dot + `StatusLabel` text (the theme `StatusBrushKey` brush is used for the dot only, so the pill stays readable over both Light and Dark theme tint families — accessibility rule).
  2. The reference "Picked up → On route → Delivered" timeline is not exposed by the VM as discrete stages (real status vocabulary: Draft/Dispatching/Dispatched/Delivered/Signed/Received); rendered as `DeliveryProgressPercent` progress bar + status instead (no state fabrication).
  3. The full editable Professional `DeliveryNoteDetailView` is hosted inside a glass inset below the reference header block (documented hybrid, GB-P6 pattern) so the delivery workflow (quantity entry, save, attachments, history) stays fully available in Personal mode.
  4. Phone affordance on the driver card is an informational chip (no call command exists on the VM — no dead button).
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-07 — Invoice Aging (GB-P10)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Reference note (honesty):** the glass-07 reference PNG (like glass-08/09) was **lost from the workspace when this session was restored** (only the repo was persisted; `/home/user/mockups-generated/` and the measurements JSON are gone). This screen was implemented against the **measured composition map recorded in GB-P1** ("filter pills (Overdue active) · 3 stat cards · invoice table + glowing aging bars") + the established token plane / component library. Re-verify against the reference file when it is re-supplied.
- **Architecture (dashboard shell restructure):** the routed Dashboard module VM is `DashboardViewModel` — a multi-page shell (`Pages` / `ActivePage` / `SelectTabCommand`, the SAME contract as the Professional `DashboardView` tab strip; pages = Overview/SheetStore/PI/JO/DN/TaxInvoice/Statement sub-VMs, all `DashboardPageBase`). `PersonalDashboardView` is now the **Personal dashboard shell**: glass page-pill strip (active pill via `ReferenceEqualityConverter` vs `ActivePage` — the Professional mechanism — `SelectTabCommand` same command+parameter) + content host `Content="{Binding ActivePage}"`. Scoped implicit templates: `OverviewDashboardViewModel` → `PersonalDashboardOverviewView` (the glass-01 surface, content moved out intact), `TiDashboardViewModel` → `PersonalTaxInvoiceDashboardView` (this screen). Other forked module pages resolve to their Personal views via GlassShell; un-forked pages fall back to Professional views (functional, documented).
- **glass-07 surface (TiDashboardViewModel contract, member-verified):** filter pill pair (All Invoices → `ViewAllInvoicesCommand` · Overdue → `ViewOverdueCommand` + `OverdueCount` badge when `HasOverdueInvoices`), 5 KPI stat cards (`KpiTiles` via the new SHARED `GlassBlush.KpiTileTemplate` — promoted to `GlassComponents.xaml` so every Personal dashboard page renders KPI tiles identically), invoice-aging card (`AgingBrackets`: Label · `AmountDisplay` · glowing bar = `NormalizedWidth` (0..1, Maximum=1) filled with the bracket's `SeverityColor` theme brush via `BrushKeyConverter` · `CountDisplay`; empty state when `!HasAgingData`), recent-invoices glass list (`RecentInvoices`: InvoiceNumber/CustomerName/IssueDateDisplay/AmountDisplay/Status+dot; virtualized Recycling ListBox; whole-row `MouseBinding` → `OpenInvoiceCommand` with the item — the Professional contract). Four-state contract (IsLoading skeleton / HasError / empty / data) preserved.
- **Deviations (documented):**
  1. The reference "3 stat cards" note vs the VM's 5 KPI tiles: all 5 are rendered (data-complete superset) — with the reference PNG lost, the specific 3-tile subset could not be re-verified; no data is dropped.
  2. Filter pills are navigation actions (the VM exposes no live filter state — both commands open the TaxInvoice module); the Overdue pill carries the overdue badge as its live indicator.
  3. `AgingMixSlices` donut + `RevenueBars` + `TopCustomers` (also on the VM) are not rendered — the recorded glass-07 composition is pills · stat cards · aging bars · invoice table; those extras remain in the Professional TI dashboard page.
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-08 — Analytics / Executive Report (GB-P11)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Reference note (honesty):** the glass-08 reference PNG was **lost from the workspace when this session was restored**. Implemented against the **measured composition map recorded in GB-P1** ("filter bar · multi-line hero chart · radial gauge · bar cards") mapped onto the data the VM actually exposes (no fabrication). Re-verify against the reference file when re-supplied.
- **Architecture (reports hub):** the routed Reports module (`"SalesReport"` and the other report keys all map to `SalesReportViewModel` with different `ActiveKey`) is a HUB: `NavItems` (ReportNavItem Key/TitleKey/Icon/SubtitleKey/Group) + `NavigateReportCommand` (parameter = Key) + `ActiveKey` string + `ActiveViewModel` content. `PersonalSalesReportView` is the Personal hub: glass report pill strip (active via `StringEqualsMultiConverter` Key vs `ActiveKey` — the Professional mechanism) + content host `Content="{Binding ActiveViewModel}"`. Scoped template: `ExecutiveReportViewModel` → `PersonalExecutiveReportView` (this screen); all other sub-reports fall back to their Professional views (functional, documented).
- **glass-08 surface (ExecutiveReportViewModel contract, member-verified):** glass filter bar (DateFrom/DateTo native DatePickers TwoWay — the Professional control; `ClearFilterCommand` "All time" · `RefreshCommand`), `PeriodDescription` header + `HasCostGap`/`CostGapNote` warning, `SummaryCards` (4 bar cards: QUOTED/INVOICED/PROFIT/OUTSTANDING — KpiItem Title/Value/Subtitle/`BrushKey` accent), the glowing **order-to-cash funnel** hero (`Funnel`: Label · SharePct bar via the Professional `PercentToWidthConverter` MultiBinding pattern (pct + track `ActualWidth`) with accent gradient + glow · SharePctDisplay), `HealthCards` (4 cards), `TopSpecifications`/`TopCustomers` glass mix rows (Label · SharePct progress · SharePctDisplay). Four-state contract (IsLoading skeleton / HasError / IsEmpty / IsContentVisible) preserved.
- **Deviations (documented):**
  1. The reference "multi-line hero chart" is rendered as the order-to-cash funnel (the VM's hero data — a multi-line chart series does not exist on the VM; no fabrication).
  2. The reference "radial gauge" has no gauge data on the VM (delivery completion exists only as a KpiItem percentage) — shown as a health card, not a fake gauge.
  3. Native `DatePicker` controls (Professional contract) inside the glass filter bar — no glass DatePicker style exists in the component library.
- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

### glass-09 — Settings / Appearance (GB-P12)
- **Status:** ✅ STATIC VERIFIED · RUNTIME NOT VERIFIED (no .NET SDK) · VISUALLY NOT YET VERIFIED.
- **Reference note (honesty):** the glass-09 reference PNG was **lost from the workspace when this session was restored**. Implemented from the GB-P1 measured composition map ("section list rail · theme tiles · accent swatches · density segmented control · motion toggle"). Re-verify against the reference file when re-supplied.
- **Architecture (Settings module):** routed Settings module = `SettingsViewModel` (`SettingsPage` string key + `SwitchSettingsPageCommand` + `NavRows` (Key/Glyph/Label/GroupLabel/IsActive) + `ActivePageLabel`). `PersonalSettingsView` mirrors the Professional shell: glass section rail (the same `NavRows`/command/IsActive mechanism) + the Professional's **lazy page host** — one `ContentControl Content="{Binding SettingsPage}"` with the **existing** `SettingsPageTemplateSelector` (reused, zero new C#): it resolves `SettingsPage.<key>` templates from this view's resources, so exactly one page tree exists at a time (the same architecture that fixed the Professional "sidebar vs main screen sync" bug).
- **glass-09 surface (Appearance page, SettingsViewModel members verified):** base-mode tiles Light/Dark (`ThemeMode` string, TwoWay — Professional's canonical path: `ThemePipeline.Rebuild` → persist → audit, one theme-change path) · visual-family tiles (`VisualThemeOptions` Key/Display, `SelectedValue` = `SelectedVisualTheme` TwoWay) · accent swatches (`AccentPalette` chips → `ApplyAccentCommand` with `Key` — the exact colour the VM applies, plus the custom-hex row: `CustomAccentHex`/`CustomAccentSwatch`/`ApplyCustomAccentCommand`/`CustomAccentStatus`) · density segmented control (`DisplayDensityOptions` = Compact/Normal/Comfortable/Spacious, `SelectedItem` = `DisplayDensity` TwoWay) · motion toggle (`ReducedMotion` TwoWay).
- **Deviations (documented):**
  1. All 11 non-Appearance pages (Themes/AI/Agent/Voice/Updates/Data/Demo/License/Features/Sharing/About) host the **Professional section UserControls** inside the glass content card (hybrid, as in P4–P10/P11) — Settings stays fully functional in Personal mode; the reference only shows the Appearance surface.
  2. The glass-09 page rail uses the Professional `SettingsNavRow` `Label` (localized at runtime) directly rather than per-row i18n keys — the rows already resolve localization in the VM.
  3. The Appearance base-mode control is a 2-tile glass ListBox bound to `ThemeMode` (the Professional page uses the same property via `AppearanceModeCard`'s segmented control — same property, glass presentation).
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
- **Known limitation (RESOLVED in GB-P15):** the MainWindow-level popups (user-switch / notifications / settings quick panel) are anchored to the Professional top-bar buttons; in Personal mode they still open but anchor to window origin. Fixed in GB-P15: experience-gated Professional popups + Personal-side anchored popups sharing the same flags.
- **Verification:** XML well-formed (MainWindow + GlassShell); brace/paren balance on all touched C# (delta vs HEAD clean); xaml_validator 0 ERROR. BUILD/TESTS NOT RUN, RUNTIME NOT VERIFIED.

## GB-P15 — Global visual consistency (2026-09-19)
- **Status:** ✅ STATIC VERIFIED · BUILD NOT RUN (no .NET SDK) · RUNTIME NOT VERIFIED.
- Two consistency items carried from earlier phases, both resolved:

### 1. Personal-side top-bar popup anchors (carried from GB-P14 limitation)
- **Problem:** the top-bar popups (Notifications, User Switch) live in `MainWindow` and are anchored to the Professional top-bar buttons. In Personal mode that top bar is collapsed, so an un-gated popup would open at the window origin instead of under the Personal button.
- **Fix (one shared mechanism, no new VM state):**
  - `Converters/ExperienceGatedConverter.cs` (new, view-only `IMultiValueConverter`), registered in `Themes/Styles/ConverterResources.xaml`. `values[0]` = the popup's open flag, `values[1]` = `IsPersonalExperience`; `ConverterParameter="Personal"` → open in Personal only, default → open in Professional only.
  - `MainWindow.xaml`: the `NotificationsPopup` and `UserSwitchPopup` `IsOpen` are now a `MultiBinding` through the gate (Professional side). The two popups that only the Professional chrome can open (Settings/Screenshot/Record/Storage) are untouched — they have no Personal-mode trigger.
  - `GlassShell.xaml`: two new Personal popups anchored to `PersonalNotificationButton` / `PersonalUserCardButton`, hosting the SAME shared panels (`ActivityPanel`, `UserSwitchPanel`), gated to Personal only, bound to the SAME `IsNotificationsOpen` / `IsUserSwitchOpen` flags — one source of truth, the gate selects which anchored copy may open.
  - `GlassShell.xaml.cs`: `Opened`/`Closed` handlers mirror `MainWindow`'s (panel `LoadAsync`/`RefreshAsync`, `RefreshCurrentUserPublic` on user-switch close).
  - `MainWindow.xaml.cs`: the click-outside guard now recognizes the Personal anchors via a visual-tree `AutomationId` walk (`Shell.PersonalNotificationsButton` / `Shell.PersonalUserCardButton`), so a Personal anchor click toggles its popup closed instead of close-then-reopen.
- No business logic touched; both experiences share the same flags and panels.

### 2. Glass error-bubble variant (carried from GB-P13)
- `GlassBlush.ChatBubbleError` added to `Themes/GlassBlush/GlassComponents.xaml` (same glass geometry as the assistant bubble, coral `GlassBlush.Danger` border + `GlassBlush.DangerSoft` surface).
- `PersonalAiChatView.xaml`: the AI bubble now keys off the model's own `IsAiResponse` flag (`!IsUser && !IsError`); error messages (`ChatMessage.IsError`) render in the coral-tinted bubble — matching the Professional view's error handling, expressed in glass.

- **Build:** NOT RUN (.NET SDK unavailable). Runtime fidelity: NOT YET VERIFIED (GB-P16).

## GB-P16 — Runtime screenshot fidelity — **NOT RUN (no .NET SDK / WPF runtime)**
- **Status:** ❌ NOT RUN. There is no .NET SDK or WPF runtime in this environment, so no build, no launch, no screenshots, and no reference-vs-actual diff are possible here.
- **What WAS delivered:** the static gate (xaml_validator 0 ERROR across all Personal + Professional XAML) and the full token/contract verification for every screen. All ten screens are **STATIC VERIFIED**; none is **RUNTIME VERIFIED** or **VISUALLY MATCHED**.
- **Verification plan for an environment with the SDK (to be run there):**
  1. `dotnet build` (0 errors) + run the unit/integration test suites.
  2. Launch at a fixed 1408×768 (16:9) canvas and 100% DPI to match the references; capture each Personal screen (glass-01…glass-10) to PNG.
  3. Diff each capture against its reference under matched canvas/aspect/DPI; fix the largest differences first, re-capture, iterate.
  4. Exercise the Professional↔Personal switch live (no restart) and confirm module state is preserved.
  5. Only then may any screen be labelled **VISUALLY MATCHED**. Until then the honest status is **NOT YET VERIFIED**.

## GB-P17 — DPI / accessibility / performance (static pass)
- **Status:** ✅ STATIC VERIFIED · BUILD NOT RUN (no .NET SDK).
- **Fix applied (accessibility):** icon-only buttons previously exposed only a `ToolTip` (not an automation Name). Added explicit `AutomationProperties.Name` to all ten icon-only buttons on the Personal plane: the six top-bar buttons in `GlassShell` (Professional UI, Notifications, AI Assistant, Theme, Settings, Account), the three in `PersonalAiChatView` (Clear chat, Close, Voice input), and the Refresh button in `PersonalExecutiveReportView`. Text-bearing buttons/nav items already derive names from their Content.
- **DPI:** WPF is DPI-virtualized — layout is expressed in logical pixels and scales with the system DPI setting; no bitmap assets and no absolute physical-pixel assumptions in the Personal views, so the composition scales uniformly. No fixed-DPI breakage. (Runtime scale testing is part of the GB-P16 plan.)
- **Performance:**
  - `GlassBlush.DataGrid` keeps `VirtualizingPanel.IsVirtualizing=True` + `VirtualizationMode=Recycling`; the glass skin adds **no per-row effects**.
  - Effects inside repeat-item templates are **bounded**, not per-thousand-row: the executive report's KPI tiles (4 SummaryCards + 4 HealthCards, `CardShadowFx`) and the order-to-cash funnel (≤5 stages, `GlowAccent`), and the sheet-store card list (single card shadow). These are small fixed sets, not virtualized data rows — within the glass performance rule.
  - Atmospheric background = 3 static gradient layers (no bitmap, no live blur, no animation by default; `ReducedMotion` respected).
- **Accessibility (contrast, measured statically against the measured dark canvas #021749):**
  | Text token | On Surface (glass) | On SurfaceFaint | WCAG |
  |---|---|---|---|
  | `GlassBlush.Text` (primary) | **6.66:1** | 9.57:1 | AA pass (≥4.5) |
  | `GlassBlush.TextMuted` (secondary) | 3.87:1 | 5.57:1 | ≥3.0 non-text/UI |
  | `GlassBlush.TextFaint` (meta/timestamp) | 3.41:1 | 4.90:1 | ≥3.0 non-text/UI |
  - Primary text clears AA comfortably. Secondary/meta tokens sit at/above the 3:1 non-text threshold but below 4.5:1 for normal body text — an inherent property of the reference's translucent glass surfaces (raising the hue enough to clear 4.5 would visibly alter the source-of-truth look). Per the accessibility rule ("if the reference has low contrast, keep usable text contrast and document"), the reference colors are **kept** and this is documented here. Meta tokens are used only for secondary/timestamp text, never primary content.
  - Keyboard: all interactive Personal controls are native Button/ToggleButton/CheckBox/DatePicker/TextBox (keyboard-focusable); scroll containers are `Focusable=False` with `FocusVisualStyle` nulled (they are not tab stops by design). Focus visibility comes from the shell's default focus visual on focusable controls.

## GB-B1 — First real build fix round (2026-09-19)
- **Context:** the first REAL build (user's .NET SDK environment — this
  sandbox has no SDK) surfaced markup-compiler errors that static
  validation could not see. Three root causes, all fixed at the source:
- **Root cause 1 (MC6002 — the big one): wrong root xmlns on 4 dictionaries.**
  `GlassBlushTokens.xaml`, `GlassComponents.xaml`, `GlassShellStyles.xaml`
  (GB-P1) and `PagePatterns.xaml` (MASTER P1) declared their root default
  namespace as `http://schemas.microsoft.com/winfx/2006/xaml` (the XAML
  *language* namespace) instead of `.../xaml/presentation`. `ResourceDictionary`
  and every WPF UI/Media type live in the **presentation** namespace, so the
  whole dictionary failed markup compilation at the root tag. This single
  bug explains the flood of downstream designer errors: all
  `XDG-0001 "GlassBlush.* could not be resolved"` (dictionaries never
  compiled → resources absent) and `XDG0008 "GlassShell type not found"`
  (build failure → assembly incomplete). Fixed: root default xmlns →
  `.../xaml/presentation` in all 4 files.
- **Root cause 2 (MC3072/XLS0413): `FontFeatures` is not a WPF property.**
  It is a CSS property (`font-feature-settings`) with no WPF XAML
  equivalent — a hallucinated attribute (19 occurrences across 5 files:
  GlassComponents KPI template + the 4 Personal views with KPI/readout
  numerals). Removed everywhere. Documented deviation: the references'
  tabular (monospaced-digit) numerals cannot be expressed in WPF XAML
  (no OpenType font-feature API on TextBlock); default proportional
  numerals render — acceptable typographic delta, no visual regression
  beyond digit advance width.
- **Root cause 3 (MC3024): property set twice — attribute + property element.**
  WPF allows a property to be set exactly once. Six sites carried both a
  `Style="..."` attribute AND a `<El.Style>` property element (the element
  was the real one, `BasedOn` the same style + triggers): `MainWindow.xaml`
  (connectivity pill — PRE-EXISTING from the MERIDIAN stream),
  `OverviewDashboardView.xaml` (overdue stat — PRE-EXISTING),
  `PersonalDeliveryNoteView.xaml` (empty-state), `PersonalSheetStoreView.xaml`
  (2 mode tabs + stock ProgressBar — GB-P8). The redundant attribute was
  removed at all six sites.
- **Errors needing NO file change (cascade / local-only):**
  - `XDG0010 "MultiValueConverter must be specified"` +
    `XDG-0001 "ExperienceGatedConverter incompatible type"` (MainWindow
    popup gates, GB-P15): designer-level type resolution — the XDG designer
    resolves `clr-namespace` types against a BUILT assembly, which a
    clean checkout has never produced. Expected to clear with the first
    successful build; the XAML syntax (`MultiBinding Converter=...`) is
    standard and the converter implements `IMultiValueConverter`.
  - `XDG0006 "DbChipStyle"` (MainWindow L137) and
    `XDG0006 "views/production/controls/productionunitdetailtemplates.xaml"`
    (lowercase pack URIs, L173–194): **not present in this branch** — no
    `DbChipStyle` anywhere in the repo, and the only reference to
    `ProductionUnitDetailTemplates.xaml` in our tree uses the correct
    pack-URI case. These belong to the local `ProGlassAutomationperf`
    working copy, which has diverged from this branch; re-sync the branch
    and they disappear.
- **Gate hardening (one fix over N edits):** `tools/xaml_validator.py`
  gained three ERROR-level checks, each verified against synthetic bad
  files — `W18` root default xmlns = XAML (non-presentation) namespace,
  `W19` non-existent WPF property denylist (`FontFeatures`), `W20`
  property set as both attribute and property element (set-once rule).
  Lesson: static XML well-formedness (ET) cannot catch namespace choice,
  property existence, or WPF set-once semantics — all three are now gates.
- **Status:** fixes committed; the build is **NOT RE-RUN here** (no SDK).
  The rebuild is due in the SDK environment — nothing in this stream may be
  labelled build-verified until it passes there.

## GB-B2 — Second build fix round (2026-09-19)
- **Context:** after the GB-B1 fixes the user's SDK environment still
  reported a full error list. Verified every listed error against this
  branch's files line-by-line; exactly **two markup-compiler root causes
  remained in this tree** — both in the Glass Blush theme dictionaries,
  both of which keep the whole dictionary from compiling and therefore
  regenerate the entire downstream cascade (hundreds of
  `XDG-0001 "GlassBlush.* could not be resolved"` + `XDG0008 "GlassShell
  does not exist"` + the `ExperienceGatedConverter` MultiBinding designer
  errors, which need a built assembly to resolve clr-namespace types).
- **Root cause 1 (MC4011): `TargetName` in an explicit Style's triggers.**
  `GlassBlush.TimelineItem` (GlassComponents.xaml) set
  `<Setter TargetName="Dot" ...>` inside `<Style.Triggers>` (two Tag
  triggers × 3 setters). `TargetName` is only legal inside a
  ControlTemplate or DataTemplate — in an explicit Style the markup
  compiler rejects it ("TargetName property cannot be set on a Style
  Setter"). Fixed: the two Tag triggers moved into the style's
  ControlTemplate as `ControlTemplate.Triggers` (same setters, same
  visual result: the dot glows for `done`/`active` timeline stages).
- **Root cause 2 (MC3072): `IsDirectionalLightOnly` on DropShadowEffect.**
  `GlassBlush.GlowAccent` (GlassShellStyles.xaml) carried
  `IsDirectionalLightOnly="False"` — no WPF effect type has that property.
  Removed; the effect is otherwise unchanged (Color/BlurRadius/
  ShadowDepth/Opacity).
- **Errors from the user's list verified NOT present in this branch
  (stale list / local-copy divergence):**
  - MC6002 (4 dictionaries) / FontFeatures (19) / MC3024 (6 set-twice
    Style) — already fixed at source in GB-B1; repo-wide re-scan on
    2026-09-19 confirms 0 occurrences of each pattern in the current
    tree.
  - `XDG0006 "DbChipStyle"` (MainWindow L137) and `XDG0006
    "views/production/controls/productionunitdetailtemplates.xaml"`
    (L173–194): neither string exists anywhere in this branch's
    `MainWindow.xaml` (or repo). These lines exist only in the local
    `ProGlassAutomationperf` working copy, which diverged from this
    branch. Re-sync the branch → they disappear.
  - `XLS0414 "System.Object not found"` + `XDG0010/XDG-0001
    ExperienceGatedConverter` + `XDG0008 GlassShell` — designer errors
    against a missing/stale built assembly; clear with the first
    successful build of the current tree.
- **Gate hardening:** `tools/xaml_validator.py` — W19 denylist gains
  `IsDirectionalLightOnly`; new **W21** (ERROR) catches any Setter with
  `TargetName` outside a ControlTemplate/DataTemplate ancestry (unit
  tested against the exact bad/good patterns).
- **Status:** validator 337 files **0 ERROR / 22 WARN (legacy) / 3 NOTE**.
  Build is **NOT RE-RUN here** (no SDK) — rebuild due in the SDK
  environment.

## GB-B3 — Third real build fix round: C# compile blocker (2026-09-19)
- **Context:** third real-build error list from the SDK environment
  (`C:\Users\DELL\source\repos\ProGlassAutomationperf`). Shape changed
  completely: **5 active C# errors, all on one line**, plus ~100 designer
  (XDG) errors.
- **Root cause (1 line → 5 CS errors):**
  `Services/Optimization/GuillotinePacker.Partitioned.cs` L227 contained
  an unparenthesized ternary inside a string interpolation:
  `$"…{bestGlobal == other ? "batch-combined" : "global"} pass…"`. C#
  forbids a conditional directly in an interpolation — the `:` terminates
  the hole — producing CS8361 + CS8076 + CS8088 + CS1003 ×2 + CS0103
  (`'global' does not exist in the current context`) on that single line.
  The break was **committed** in this branch (carried in by `fe760e4`,
  opt-workstream commit) and invisible until now: while XAML
  markup-compilation was still failing (pre-GB-B1/GB-B2), MSBuild never
  reached C# compilation. Once markup compiles cleanly, the compiler runs
  and hits this line.
- **Fix:** parenthesize the conditional —
  `{(bestGlobal == other ? "batch-combined" : "global")}`. Identical
  runtime message text, zero business-logic change (message string only).
- **Triage of the rest of the list — all cascade, no XAML defects:**
  - ~70 × XDG0008 "name does not exist in clr-namespace" for the
    project's own types (all Professional VMs/views in MainWindow.xaml
    L137–210, TrendChartControl, DonutChartControl,
    SmoothScrollBehavior/SmartScrollBubblingBehavior): when the assembly
    fails to compile, the designer cannot resolve ANY clr-namespace type
    in it. Every named type was grep-verified to exist in the repo
    (views, VMs, both chart controls, both behaviors in `Behaviors/`).
  - XDG-0001 "incompatible type" for
    `InverseBoolToVisibilityConverter` (Overview L241/400) and
    `BrushKeyConverter` (L373): both classes exist
    (`Converters/*.cs`) and both keys are declared in
    `Themes/Styles/ConverterResources.xaml` (merged FIRST in App.xaml
    L20, app-level scope) — the designer error is the failed-assembly
    cascade, not a missing/mistyped resource.
  - 11 × XDG0010 "Operation is not valid due to the current state" at
    behavior-attached-property sites in PersonalProductionControlCenterView:
    same cascade (behavior types unresolved).
- **Sweep for the same bug class:** repo-wide scan for unparenthesized
  ternaries inside interpolations across all `.cs` — the fixed line is
  the only real instance (2 scanner hits in
  SettingsViewModel.ThemeWallpaper.cs were null-coalescing + format
  specifiers, valid C#).
- **Status:** fixed at source, committed `dc71201`, **PUSHED**.
  C# compilation is **NOT RUN here** (no SDK) — the parenthesization is
  the documented, canonical fix for CS8361; rebuild due in the SDK
  environment. After a successful compile, the full XDG block is
  expected to clear on its own (designer caches the stale failed
  assembly — a clean rebuild is the correct remedy, not XAML edits).

## GB-B4 — Fourth real build fix round: missing using (2026-09-19)
- **Context:** fourth real-build error list (80 errors). The 5 GuillotinePacker
  CS errors from GB-B3 are GONE → `dc71201` confirmed effective. New shape:
  3 active C# errors in ONE file + 77 designer errors.
- **Root cause (1 line → 3 CS errors):**
  `Views/Personal/PersonalAiChatView.xaml.cs` (GB-P13 code-behind) used
  `NotifyCollectionChangedEventArgs` in the handler signature (L43) without
  importing `System.Collections.Specialized` (the file had only
  `using System.Collections;`). Verified against Microsoft Learn (view=net-8.0):
  on net8.0 the type is still `System.Collections.Specialized.*` — it never
  moved to `System.Collections`. CS0246 at L43 + CS0123 at L39/L55
  ("no overload matches `NotifyCollectionChangedEventHandler`") are all one
  root cause: the handler's parameter type failed to resolve.
- **Fix:** added `using System.Collections.Specialized;` — identical to the
  Professional sibling `Views/AI/AiChatView.xaml.cs` (same event, compiles).
  No ambiguity risk: no other name in the file exists in two imported
  namespaces.
- **Cross-checks (before pushing):**
  - all 20 repo files using `NotifyCollectionChanged*` audit: every one
    imports Specialized or fully qualifies — no second instance of the bug.
  - all 54 unique XDG0008 type names (63 list entries) verified to exist in
    the EXACT namespace quoted by each error (scripted class-declaration
    index vs the error list).
  - XDG-0001 ×3: both converter classes exist; both keys declared in
    `ConverterResources.xaml` (merged FIRST in App.xaml, app scope).
  - XDG0010 ×11: both behavior classes exist in `Behaviors/`; csproj has no
    exclusion covering any involved file.
  - **line-number sync:** every error line in the user's list matches the
    current branch files → their build is on this exact branch (no stale
    copy this round).
- **Status:** fixed, committed + pushed. C# compile NOT RUN here (no SDK);
  compiler gate = user's rebuild. Expected result: 0 active C# errors
  (csc is exhaustive — 3 was the complete set) and the full 77-error XDG
  block clearing on a clean rebuild (designer cache of the failed assembly).

## GB-B5 — First RUNTIME crash: invalid star Length in GlassShell (2026-09-19)
- **Context:** the build is now GREEN (GB-B3/B4 verified effective — the app
  compiles, starts, opens the DB, applies theme Monet, signs in). First
  crash: `XamlParseException` from `GlassShell` BAML parse during
  `MainWindow.InitializeComponent` → **every startup, both experiences**
  (GlassShell BAML is loaded from MainWindow unconditionally).
  Inner: `FormatException: '0.94*' string cannot be converted to Length`
  (LengthConverter), GlassShell line 479.
- **Root cause:** the GB-P13 AI overlay in `Views/Personal/GlassShell.xaml`
  used `MaxWidth="0.94*" MaxHeight="0.94*"` intending "94% of the window".
  Star units are legal ONLY on `ColumnDefinition.Width` /
  `RowDefinition.Height` (GridLength). On any Length-typed property the
  value is parsed by `LengthConverter` — which throws. Crucially, markup
  COMPILATION does not catch this (the string is stored in BAML); it only
  fails at RUNTIME parse of the BAML — invisible to the MSBuild gate and to
  the structural validator.
- **Fix (design intent preserved):** new `Converters/ScaleLengthConverter`
  (bound Length × constant `ConverterParameter` factor; null/NaN-safe,
  registered in `ConverterResources.xaml` per its house rule) +
  `MaxWidth/MaxHeight = Binding Window.ActualWidth/ActualHeight` (RelativeSource
  AncestorType=Window) × 0.94. Degraded fallback (no Window ancestor, e.g.
  designer) → NaN → element keeps its fixed 920×640.
- **Class sweep (before push):** repo-wide scan of all 337 XAML files for
  star values on Length-typed properties: the 2 GlassShell sites were the
  ONLY invalid ones (663 legitimate Grid star sites untouched); scan for
  CSS-style units (`%`, `px`, `vh`, `vw`, `rem`) on length attributes:
  zero hits.
- **Gate hardened:** validator **W22 (ERROR)** — star GridLength on any
  property except ColumnDefinition.Width/RowDefinition.Height (attribute
  + Setter form). Unit-tested: synthetic `Border MaxWidth="0.94*"` flagged;
  `ColumnDefinition Width="0.94*"`/`RowDefinition Height="2*"` clean.
  Full run: 337 files **0 ERROR / 22 WARN (legacy) / 3 NOTE**.
- **Honest scope:** static sweep covers the reported crash class + CSS-unit
  values. Other runtime parse bombs (invalid colors, converter exceptions
  on switch-to-Personal) are not statically provable — the next runtime
  pass is the user's.

## GB-B5b — CS0119 in ScaleLengthConverter + stale-copy evidence (2026-09-19)
- **Context:** build after GB-B5 returned 2 ACTIVE CS errors + 9 XDG errors
  + a crash report identical to the previous one.
- **CS0119 ×2 (L52/L61) — my bug, root-caused:** inside
  `ScaleLengthConverter`, the simple name `Convert` binds to the class's
  own `Convert(object,Type,object,CultureInfo)` member (C# name lookup:
  current-type members beat namespaces/types in scope), so
  `Convert.ToString(...)` was a method-group access →
  "is a method, which is not valid in the given context". Fix:
  `System.Convert.ToString` (both sites). Plus a `length == 0 → NaN`
  guard (Window.ActualWidth before first layout; a 0-px MaxWidth would
  hide the overlay for a frame). Cross-checked before push: full-file
  identifier audit, repo-wide scan for any other Convert-member +
  bare-Convert.X file (none), validator 337 files 0 ERROR.
- **Crash report ERR-9ABC30 is STALE (proven, not assumed):** it still
  shows `0.94*` in GlassShell BAML. With CS0119 active, MSBuild produced
  no new exe — the app that crashed was the previous (GB-B4-era) binary,
  which still contained `0.94*`. Timestamp 09:39 > pull time is consistent
  with "build failed → old exe ran".
- **XDG0003 '0.94*' at MainWindow.xaml L283 (proven):** L283 of the branch
  is exactly `<viewsPersonal:GlassShell ...>` — the designer parses the
  GlassShell BAML from that reference, so the error attributes to the
  reference site. It proves the user's COPY still has the old GlassShell
  even though ScaleLengthConverter.cs (same commit c46659d) is present →
  their pull is partial/conflicted on that file (or designer BAML cache).
  Verification commands given to the user (git status/diff, findstr).
- **XDG0006 ×8 'views/production/controls/productionunitdetailtemplates'
  (triaged):** the file EXISTS on the branch
  (`Views/Production/Controls/ProductionUnitDetailTemplates.xaml`,
  PascalCase, committed in base 9716050 — pre-existing, long-shipped),
  referenced correctly from ProductionUnitDetailControl.xaml L30; no
  csproj exclusion; the lowercase in the error is .NET's lowercased
  resource-KEY display, not a casing bug. Error attributed to the
  MainWindow DataTemplate lines (L173–194) where the consuming production
  views are referenced (via ProductionStationBody). In a clean branch
  build this resolves; in the user's copy the file/assembly is stale or
  missing — same partial-sync signature as above.
- **Status:** CS0119 fixed at source (e324bf4), pushed. C# compile NOT
  RUN here (no SDK); the compile is the user's rebuild. The XDG block is
  expected to clear once the copy is in sync AND a clean build succeeds.

## GB-B6 — Second runtime crash: container-style type mismatch (2026-09-19)
- **Context:** build GREEN, GlassShell crash GONE — the app now reaches
  sign-in and dies on FIRST LAYOUT with `InvalidOperationException: A
  style intended for type 'ContentPresenter' cannot be applied to type
  'ContentControl'` from `ItemsControl.ApplyItemContainerStyle`
  (UniformGrid → GenerateChildren).
- **Root cause (verified against .NET 8 WPF source, ItemsControl.cs):**
  `ItemsControl.IsItemItsOwnContainerOverride` returns **`item is
  UIElement`** — so an ItemsControl with explicit element children
  (`<ContentControl …/>` skeleton placeholders) uses those children as
  their OWN containers, and `ApplyItemContainerStyle` validates
  `style.TargetType.IsInstanceOfType(container)` — a
  ContentPresenter-targeted ItemContainerStyle on such a control throws
  at first layout. Markup compile and the designer do NOT catch it.
- **Defect (one style → 32 sites):** `PgSkeletonKpiBandStyle`
  (ProfessionalCore.xaml L890) was `BasedOn="PgKpiBandStyle"` WITHOUT
  overriding its `ItemContainerStyle` → inherited the
  ContentPresenter-targeted container style, while every consumer feeds
  it explicit `<ContentControl>` children. The sibling
  `PgPipelineSkeletonItemsStyle` does override it (TargetType=
  ContentControl) — the correct pattern existed, this one style missed
  it. Affected consumers (all 7 dashboards' skeletons): Overview ×8,
  Dn/Jo/Pi/SheetStore/St/Ti ×4 each = **32 sites**.
- **Fix (ONE source change):** `PgSkeletonKpiBandStyle` now overrides
  `ItemContainerStyle` with `TargetType="ContentControl"` (same Margin +
  Stretch setters as the base) — the single fix clears all 32 sites.
- **Sweep (before push):** scripted all 337 XAML files — every
  ItemsControl with element children vs its effective ItemContainerStyle
  TargetType (resolving keyed styles + BasedOn chains): 32 problems,
  ALL this one style; re-sweep after fix = 0.
- **Gate hardened:** validator **W23 (ERROR)** — same cross-file style
  resolution, flags any ItemsControl whose element children are not
  covered by the effective container style TargetType. Unit-tested:
  synthetic BasedOn-inheritance case flagged, overridden case clean.
  Full run 337 files **0 ERROR / 22 WARN (legacy) / 3 NOTE**.
- **Honest scope:** static + source-verified; the first-layout runtime
  is the user's run. If the next crash is in the same family, W23 should
  have caught it — report anything new.

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
| GB-P8 sheet store (glass-05) | ✅ STATIC VERIFIED (PersonalSheetStoreView, glass card grid; runtime NOT VERIFIED) | `GB-P8` |
| GB-P9 delivery note (glass-06) | ✅ STATIC VERIFIED (PersonalDeliveryNoteView, list + glass detail; runtime NOT VERIFIED) | `GB-P9` |
| GB-P10 invoice aging (glass-07) + dashboard shell | ✅ STATIC VERIFIED (PersonalDashboardView shell + Overview/TiAging pages; runtime NOT VERIFIED) | `GB-P10` |
| GB-P11 analytics (glass-08) + reports hub | ✅ STATIC VERIFIED (PersonalSalesReportView hub + ExecutiveReport glass-08; runtime NOT VERIFIED) | `GB-P11` |
| GB-P12 settings (glass-09) | ✅ STATIC VERIFIED (PersonalSettingsView rail + Appearance glass-09 + 11 Professional section hosts; runtime NOT VERIFIED) | `GB-P12` |
| GB-P15 global visual consistency | ✅ STATIC VERIFIED (Personal-side popup anchors + error-bubble variant; BUILD NOT RUN) | `GB-P15` |
| GB-P16 runtime screenshot fidelity | ❌ **NOT RUN — no WPF runtime in this environment** (verification plan documented) | — |
| GB-P17 DPI/a11y/performance | ✅ STATIC VERIFIED (automation names on 10 icon buttons; contrast measured+documented; virtualization/effects audit) | `GB-P17` |
| GB-B1 first real build fix round | ✅ FIXED at source (4 root-xmlns + 19 FontFeatures + 6 set-twice Style; validator +W18/W19/W20) — rebuild due in SDK env | `GB-B1` |
| GB-B2 second build fix round | ✅ FIXED at source (TimelineItem TargetName → ControlTemplate triggers = MC4011; removed IsDirectionalLightOnly = MC3072; validator +W21, W19 denylist); rest of user's list verified stale/local-copy | `GB-B2` |
| GB-B3 third build fix round — C# blocker | ✅ FIXED at source (unparenthesized ternary in interpolation, GuillotinePacker.Partitioned.cs L227 = CS8361 + 4 cascade CS errors; ~100 XDG errors = failed-assembly cascade, types/resources grep-verified); C# compile NOT RUN (no SDK) | `dc71201` |
| GB-B4 fourth build fix round — missing using | ✅ FIXED at source (PersonalAiChatView.xaml.cs: `using System.Collections.Specialized;` = CS0246 + 2×CS0123, one root line; 77 XDG = cascade, all 54 types + 2 converters + 2 behaviors verified; line-sync proves user builds this branch); C# compile NOT RUN (no SDK) | `3c4dc23` |
| GB-B5 first runtime crash — star Length in GlassShell | ✅ FIXED at source (MaxWidth/MaxHeight="0.94*" → ScaleLengthConverter × 0.94 of Window Actual; repo-wide sweep: only 2 invalid star sites, 663 legit Grid stars; validator +W22 unit-tested; BAML runtime parse NOT RUN — no SDK) | `c46659d` |
| GB-B5b CS0119 in my converter + stale-copy triage | ✅ FIXED at source (Convert.ToString shadowed by the Convert member → System.Convert.ToString ×2; zero-length guard; full identifier audit + repo-wide same-class scan; crash report proven stale = old exe; XDG0003/XDG0006 proven copy-divergence with verification commands) | `e324bf4` |
| GB-B6 second runtime crash — container-style type mismatch | ✅ FIXED at source (PgSkeletonKpiBandStyle inherited ContentPresenter ItemContainerStyle via BasedOn, applied to explicit ContentControl item containers — .NET 8 IsItemItsOwnContainerOverride='item is UIElement'; ONE style fix clears 32 sites/7 dashboards; validator +W23 unit-tested; first-layout runtime NOT RUN) | `cb54d1b` |
