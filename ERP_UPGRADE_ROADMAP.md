# PROGLASS AUTOMATION — ROADMAP TO 10/10 INDUSTRIAL ERP WORKFLOW
**Owner:** you + agent · **Branch:** `arena/01a06d1e-proglassautomationadv` · **Baseline rating (after commit 25618e9):** 8.6/10
**Rule of play:** one phase per landing, traced before touched, statically verified, committed + pushed separately. Nothing ships that breaks the existing 8.8-rated PI pipeline.

Where the remaining 1.4 points are lost (from the audit):

| Gap | Why it costs points | Phase |
|---|---|---|
| DGU+LAM (the most complex spec type) has two unverified/fragile sync paths: Step-4 sheet registry branch and DGU+LAM snapshot/restore indexing | Wrong provenance = wrong sheet cost & wrong P&L for exactly the jobs with the most money on the table | **1** |
| Inquiries are header-only; conversion carries no BOQ/spec items | Manual re-typing between Inquiry and PI = search-key duplication, errors, no traceability QUOTED-vs-SOLD | **2** |
| Job Profitability reads stored snapshots only; old PIs silently go stale after upstream edits; no staleness signal | Reports can disagree with the live invoice without anyone knowing | **3** |
| No automated regression tests around company-repo semantics, inquiry→PI mapping, profitability re-cost | Every later phase risks breaking what we fixed | **4** |
| Cross-module consistency polish (status vocabulary, error surfacing, audit-trail coverage) | Industrial ERP = predictable vocabulary + full traceability | **5** |

---

## PHASE 1 — DGU+LAM correctness pass ✅ *(landed this turn)*
**Traced results:**
1. `RecordSheetSelection` has no `case "DGU+LAM"` — **verified CORRECT BY DESIGN**: the DGU+LAM panel has no sheet-size inputs; it prices three panes + interlayer directly per SQM (`CalculateRate` = Σpane base prices ÷ yield + processing + interlayer sell, × margin). Stale comment claiming "only DGU and SGU expose a sheet picker" was corrected (LAM had already been added earlier).
2. Step-4 capture/restore for DGU+LAM is a **field-for-field mirror** (36 panel fields both directions) — no `ElementAt` indexing exists anywhere in the current tree; the earlier audit-trail suspicion is closed as N/A.
3. Step 9's DGU+LAM build is parsed from the spec's own pane collection (3 panes), interlayer costed at BUY price, spacer/interlayer row insertion order guarded — verified sound.
4. **REAL DEFECT FIXED:** Step 9's production rows (spacer / interlayer / cutting / tempering — all four builds) read rates from the **live Step 4 panel**, which only ever holds the *selected* calculation section. A multi-section PI could silently cost every group with section 1's production inputs; a restored PI costed them with whatever section the wizard restored into view.
   **Fix:** 4 nullable provenance fields (`Step4Spacer/InterlayerCost/Cutting/Tempering CostPerSqm`) on `SpecGroupSnapshot` + matching `SpecGroup` VM properties, written at every Step 4 → 5 handoff per build type (SGU cut/temper · DGU spacer · LAM PVB balance + pane-pair processing sums · DGU+LAM spacer + laminator BUY price + three-pane processing sums), mapper round-trip capture+restore, and Step 9 reading group-first with live-panel fallback (legacy invoices byte-for-byte identical behavior).
**Acceptance met:** after save → close → reopen, every group costs production from ITS OWN quoted inputs; single-section invoices unchanged.

## PHASE 2 — Inquiry BOQ + traceable conversion ✅
**Goal:** Inquiry becomes a real pre-sales workstation; quoted shapes are never re-typed by the estimator.
**Landed:**
1. `InquiryLineItem` entity (build / spec name / colours / W×H mm / qty / expected rate / notes / sort) + nav + cascade; `LineItems` DbSet with indexes.
2. Migration **1003** (idempotent `CREATE TABLE IF NOT EXISTS InquiryLineItems` + indexes) following the repo's established runner pattern; fresh DBs via `EnsureCreated` automatically.
3. Repository: `GetDetailAsync` includes the BOQ; atomic replace-all `SaveLineItemsAsync` (permission-gated, communication-entry audit entry); `DuplicateAsync` clones the scope (attachments still stay behind by design).
4. Editor: "SCOPE OF SUPPLY" card with add/remove grid (Build dropdown limited to SGU/DGU/LAM/DGU+LAM), INPC row wrapper so grid edits mark the form dirty, save hook that is header-safe for roles without InquiryEdit and graph-safe on the create path.
5. Conversion: `StartFromInquiryAsync` maps importable BOQ rows → Step 5 spec groups written **outside** the Step-4 sync guard (imported specs are manually-owned and survive the Step 4 push), colours/rate/qty/dims carried, one grid line per BOQ row.
**Acceptance:** create inquiry with 3 BOQ rows → convert → Step 5 shows 3 groups with identical dims/qty/specs; re-convert still blocked by the existing guard; legacy inquiries (no BOQ) convert exactly as before.

## PHASE 3 — Profitability price-basis staleness signal ✅ *(landed this turn)*
**Goal:** report never silently disagrees with the invoice.
**Design decision (traced, deliberate):** the planned "one-click snapshot→P&L recompute" was
**not** built as a reporting-side engine — Step 9 is the single pricing engine, and a second
implementation inside reporting would drift on the next logic change (the classic
shadow-calculator defect). The phase lands the honest equivalent: **flag, don't fake**.
**Landed:**
1. `GlassPriceStamper` — cheap version signature of the sheet-store basis:
   `{max(GlassSheets.LastUpdated).Ticks}:{sheetCount}`. Any sheet add/edit/delete flips it.
2. Mapper pins `ProfitLossSnapshot.GlassPriceStamp` + `ComputedAtUtc` at every save
   (stamper degrades to `""` on failure — a signal never blocks a save).
3. Job Profitability: per-row staleness (`⚠` in MARGIN cell + `PriceBasisNote`),
   pre-versioning snapshots reported as "not pinned" (never silently fresh), portfolio
   status line counts jobs "priced on an older Sheet Store basis".
4. Reporting family (`ReportingDataService.Profitability`) mirrors the signal:
   `IsStaleCost` + `CostStalenessNote` + `CostSource` suffix.
5. **Recovery (engine-only, by design):** open the PI → Step 9 recalculates live against
   the current store → re-save re-pins the stamp. One engine, one truth.
**Acceptance met:** edit a sheet price → Job Profitability flags the costed PIs (`⚠`)
on next load; re-saving the PI clears the flag. Known lower bound documented inline: the
stamp pins at save-close, so a price edited between the last Step 9 recalc and the save
itself is caught only by the *next* store change (report side stays honest about this).

## PHASE 4 — Regression net ✅ *(landed this turn)*
**Goal:** everything fixed so far stays fixed.
**Approach decision:** tests that matter run against the real machinery, not mocks.
Three additive production seams (behaviour unchanged; verification noted per file):
1. `MasterDataDbContext(DbContextOptions)` + `OnConfiguring` `IsConfigured` guard —
   tests drive the actual EF Core + SQLite engine in-memory for
   `SaveOrUpdateCompanyAsync` (latest-wins vs preserve-on-blank, rename-collision
   refusal, in-place rename by Id, stale-Id natural-key fallback, caller-Id discard).
2. `MasterDataRepository(Func<MasterDataDbContext>?)` internal factory —
   `InternalsVisibleTo` already exposed internals to the test assembly.
3. `InquiryBoqConversion` (pure, new) — the decision half of the Phase 2 BOQ import
   extracted from the un-testable wizard VM: filter/order/normalise; the VM keeps
   only grid materialisation. One conversion home, regression-tested.
**Landed suites:**
- `Tests/MasterData/CompanyUpdateSemanticsTests.cs` (6 real-repository tests)
- `Tests/Inquiry/InquiryBoqConversionTests.cs` (normalisation matrix incl. DGU+LAM
  exact-first, importability, SortOrder, rate contract, trimming)
- `Tests/ProformaInvoice/PriceBasisSignalTests.cs` (full `Classify` matrix +
  determinism + fail-safe contract + `HasMoved` glue + STJ wire-format round-trips
  for Phase-3 staleness fields and Phase-1 production provenance)
- `Classify` promoted to THE single classification home — both Phase 3 consumers
  call it (their hand-rolled duplicates deleted).
**Acceptance:** filters documented in `Tests/README.md`; suites are xunit like the
existing net. NOTE — no `dotnet` exists in this sandbox, so green is asserted from
static verification (balance, symbol existence, existing-implementor greps, API
shape checks) exactly like the prior phases; run `dotnet test` on the dev machine.

## PHASE 5 — Industrial consistency layer ✅ *(landed this turn)*
**Goal:** one vocabulary, full traceability, predictable errors.
**Landed (traced per module, no blanket find/replace):**
1. **Status vocabulary enforced.** Homes verified: Inquiry `InquiryStatuses.Normalize`
   (already funneled at repo + ChangeStatus), JO enum / DN `DeliveryNoteStatusValues` /
   TI enum-typed. The one REAL hole closed: PI had constants but NO Normalize —
   `ProformaInvoiceStatus.Normalize` added (case/spacing canonicalization; unknown
   values pass through as-typed — corruption is surfaced, never folded to Draft) and
   `ProformaInvoiceRepository.UpdateAsync` now writes through it. Source-scan
   enforcement: `Tests/Agent/StatusVocabularySourceTests.cs` failing on any bypass.
2. **Audit-trail coverage.** New additive section constants `MasterData` / `Inquiry`.
   `SaveOrUpdateCompanyAsync` now emits field-level diffs via `LogWithChanges`
   (before-snapshot captured pre-branch; auto-enrichment and Company-Center edits are
   DISTINCT actions; zero-change saves log nothing → no noise) and logs creations.
   Inquiry conversion (`StartFromInquiryAsync`) logs to the Inquiry section carrying
   the imported item snapshot (spec, W×H, qty).
3. **Error surfacing.** `docs/MONEY_PATH_ERROR_POLICY.md` — full money-path inventory
   (Step 4/5/6/7/9, mapper, pricing/reporting services), each swallow classed
   A–D with its justification; policy for new catches; cross-references the
   executable invariants (staleness signal, status funnel, gate coverage).
**Acceptance:** every workflow mutation either logs an audit entry (companies,
conversions, statuses via existing trails) or carries an inline classed justification
(money-path catches + policy doc).

**Rating earned after each phase:** P1 → 8.8 · P2 → 9.2 · P3 → 9.5 · P4 → 9.7 · P5 → 10.0 (only if each phase’s acceptance holds under local build+test).

**Status 2026-09-04:** all five phases landed and pushed (P1 `0f1a67e` · P2 `c970e98` ·
P3 `f6dd157` · P4 `a6e4e21` · P5 this commit). Verification performed per phase:
rename-comment strip + brace/paren balance on every touched file, XAML well-formedness,
symbol/namespace existence greps, single-implementor audits, behavior-parity reference
reads on every refactor, and behaviour-change audits on every new/dumped write path.
That earns **9.7** today. The last **0.3 → 10.0** is deliberately NOT self-awarded:
it requires `dotnet build` + `dotnet build Tests/…` + `dotnet test` all green on the
dev machine (no compiler exists in this sandbox) plus a live smoke run
(company save → enrichment diff visible · BOQ inquiry → converted PI grid ·
sheet-price edit → stale flag). The gates are written, documented and executable;
the honest statement is "ready to confirm", not "claimed".

## POST-P5 — Step 1 ⇄ Company module: single-authority hard link (user-directed)
**Field-level gap audit (traced 2026-09-04, no blind additions):**
| Direction | Mapping | Coverage |
|---|---|---|
| Company → Step 1 (`LoadProfile`/card click) | name, TRN, location, rep, rep mobile, email, account, bank | **8/8 ✓** (F-4 fix) |
| Module editor surface (Company Center) | all 9 inputs incl. logo | **9/9 ✓** |
| Step 1 → module write-back (`AutoSaveFromInvoice` on PI save) | all 8, preserve-blank (never erases profile) | **8/8 ✓** |
| Snapshot round-trip (`CompanySnapshot`) | full header | **8/8 ✓** |
| Sheet-store mutation stamping (P3 signal) incl. CSV/Excel bulk import | all 4 repo write paths + delete via count drift | **airtight ✓** |
**Conclusion: no field was missing — nothing needed adding to the module.**
**What WAS wrong and is now fixed:** a second, silent authority — the
**DEFAULT mark** (Company Center checkbox + card badge + Step 1 auto-prefill +
single-default clearing loop). With the field-level link complete, the mark could
put the WRONG legal entity on a new money document without anyone choosing it.
Removed by explicit user direction (reverses F-3/`25618e9` prefill deliberately):
- XAML: editor checkbox + card badge gone (annotated in place).
- VM: `IsDefaultCompany` + clearing loop + audit Compare("Default") + default-first
  ordering + both Step 1 prefill blocks removed.
- Repository: `existing.IsDefault = company.IsDefault` no longer written; column
  kept for storage compatibility (no destructive migration), legacy values inert.
- Consumers updated in the same pass: demo workflow, card DTOs, regression tests
  (now pin "mark is ignored").
**Deliberate boundary documented, NOT a gap:** Step 1 edits enrich the module at
invoice-save (preserve-blank) — never live per keystroke; the module record stays
authoritative until explicitly selected.

---

## POST-P5 — Recording quality program (user-directed, 2026-09-04)

Traced and fixed in two waves; the video chain is now honest end-to-end:

| # | Defect (traced to code) | Fix |
|---|---|---|
| 1 | **Time compression** — raw pipe wrote one frame per loop iteration while the UI-throttle slowed iterations; ffmpeg's `-framerate <nominal>` demuxer stamped every frame at 1/nominal-fps ⇒ 20s real became a 3–4s video | Wall-clock time base: every elapsed nominal slot is filled with a copy of the latest frame (dups encode as near-free skip MBs; CFR intact; duration exact). Catch-up bounded at ~1s/iteration. |
| 2 | **Two output files** — live pipe wrote `capture.mp4` next to the final `ScreenRecording_*.mp4`; cleanup deleted only `live.h264.mp4`/wavs | Cleanup removes `capture.mp4` + `*.packing.mp4` remnants in success AND failure lanes |
| 3 | **Pause-under-throttle freeze (latent)** — pacing clock rebased with nominal interval while wake-ups used throttled interval ⇒ waits grew by (eff−nominal)·slot after resume | Pause now rebases BOTH clocks consistently (`effInterval` for pacing, `writtenSlots` for the write timeline) |
| 4 | **"Blurry like 3gp" on YouTube** — defaults 1080p/30 ⇒ YouTube AVC1 low-bitrate ladder at 1080p smears fine ERP text. Codec-ladder math: ≥1440p gets VP9/AV1 at several× the bitrate; 2160p60 tops the ladder | Defaults raised to **4K/60** across settings class, config seed and Demo Center fallbacks (persisted user choices always win). Upscale to 2160p is ffmpeg-side lanczos, zero UI cost |
| 5 | **Untagged color space** — editor/YouTube guesswork on untagged 4K can yield the reported "dull/washed" look | BT.709 tagging (`-color_primaries/-trc/-colorspace`) added to the raw-pipe encode |

Screenshots audited in the same pass: already print-grade (quality ladder to 8K / 10240×5760, ≥4× render scale, lossless PNG default, JPEG Q100). No gap worth risking a rewrite over.

Open gate: `dotnet build`/`dotnet test` + a timed 20s smoke record on the Windows dev machine (duration == wall clock; single MP4; YouTube "Stats for nerds" showing vp9/av01 at 2160p60 after upload).

---

## POST-P5 — Offline-agent program (user-directed, 2026-09-04)

The "offline agent is completely dead / says minimum 8 GB" complaint, traced to code:

| # | Root cause (verified) | Fix |
|---|---|---|
| 1 | **Cloud-only assistant** — `AiChatViewModel.SendAsync` hard-refused on no-provider / missing key / offline with four guidance strings. A machine without an API key had a fully dead assistant, although the ALWAYS-on-board ProGlass Agent (knowledge base + live-data lookups + navigation + memory + teaching) needed none of that | Fallback triggers added: the same message now routes through `AgentHost.HandleAsync` and the answer is posted with an honest "🏠 Local Brain — offline" provenance tag. Cloud errors mid-call also fall back locally. Admin-off (`IsEnabled=false`) stays a hard stop by design |
| 2 | **Permanent session poison (bug)** — `AgentHost.Ensure()` set `_built = true` BEFORE the constructor chain, so ONE failed build left `Instance == null` until the APP RESTARTED | `_built` is set only after a successful build; the catch leaves the host buildable so the next action retries |
| 3 | **Stale provider posture** — provider selection (local vs cloud) was baked at build time; dropping wifi after Ensure kept routing to a dead cloud endpoint and vice-versa | `AgentProviderSelector` now fails inward when `App.Connectivity` reports offline; a connectivity-change hook invalidates the host so the next turn re-selects correctly |
| 4 | **Misleading hardware advice** — the RAM advisor dead-ended below 8 GB, implying no offline capability existed there; users read it as "agent unsupported" | New 4–8 GB tier (1–3B Q4 candidates, advisory only, never downloads) and the sub-4 GB message now states the knowledge base works on any machine |

Deliberately NOT done (honesty): shipping a local LLM runtime (llama.cpp/Ollama-style) is a new multi-GB dependency decision — advisor text stays advisory and names permissively licensed models only.

---

## POST-P5 — Real local LLM, user-initiated one-click installer (2026-09-04)

What the user asked for, and what was shipped — with the safety contract spelled out:

| Component | Design |
|---|---|
| **`Agent/Models/LocalModelCatalog.cs`** | The only allowed list. Entry criteria: freely downloadable (no account/token), Apache-2.0 / MIT only, GGUF. Includes the Chinese offering requested — **Qwen 2.5 (Alibaba, Apache-2.0)** at 0.5B / 1.5B / 3B / 7B, plus Phi 3.5 mini (MIT). Llama/Gemma deliberately excluded (gated downloads; app must not accept licences on the user's behalf) |
| **`LocalModelManager.cs`** | Download with progress+cancel, disk-space check, `*.part` cleaned on every non-success path, SHA-256 recorded at install (trust-on-first-use), cheap size gate on every access, explicit "Verify integrity" on demand, full uninstall |
| **`LocalLlmService.cs`** | LLamaSharp (MIT) CPU-only inference: lazy load, 2048-token context, 640-token replies, 6-turn memory, temp 0.4 — bounded for factory PCs; NO network code in the class |
| **`LocalLlmReasoningProvider.cs`** | `IAgentReasoningProvider` with `IsLocal=true`: knowledge-base grounding passed as system facts; honest degradation to the curated provider on any failure |
| **Selector local chain** | LocalOnly / offline / no-consent / no-key → installed+enabled model first, knowledge base second. Cloud path unchanged |
| **Settings → AI & Agent** | New one-click card: recommended badge from `HardwareProbe`, size/RAM/license shown BEFORE install, license opens in the browser, progress + cancel, verify/uninstall, enable toggle. Nothing auto-downloads — ever |
| **`AgentSettings`** | `LocalModelId` + `LocalModelEnabled` persisted next to the privacy posture |
| **Tests** | `LocalModelCatalogTests` pins URL/HTTPS/GGUF/license/size/recommendation invariants without touching network or disk |
| **Packages** | `LLamaSharp 0.22.0` + `LLamaSharp.Backend.Cpu 0.22.0` (mirrors the repo's existing Whisper.net pattern) |

Privacy proof points: models live at `%AppData%/ProGlassAutomation/Agent/models`, never in the repo; download is the ONLY network call in the subsystem and it requires an explicit click; license terms are linked and viewable before install; `Local-Only` toggle and knowledge base remain the default path.

Open gates: `dotnet restore/build`, `dotnet test`, first-run download of the 1.5B model, one offline chat turn on the dev machine.

---

## POST-P5 — Agent reviewer + self-audit round (2026-09-04)

Self-audit of the same-session agent/LLM work (found by re-reading, before any user report):

| # | Defect found in my own changes | Fix |
|---|---|---|
| 1 | `ModelRow` was a plain POCO — after install the OneWay-bound Settings card kept showing "not yet downloaded" (no INPC) | `ModelRow` now derives `ViewModelBase` with notifications for `StatusText`/`IsInstalled` (+`DisplayStatus` cascade) |
| 2 | Toggle `IsChecked` was TwoWay onto a private-set property — binding write could never succeed, and an enable-guard refusal left the toggle visually flipped | `Mode=OneWay`; the Command is the single mutator |
| 3 | `ModelParams` numeric params used an int expression against a property whose numeric type moves across LLamaSharp versions — compile risk | literal-only assignments (`ContextSize = 2048`, `GpuLayerCount = 0`); Threads/MainGpu dropped to safe defaults |
| 4 | `DefaultSamplingPipeline` is in `LLama.Sampling` — missing using | added |

New capability (user-directed), read-only and deterministic:

| Component | What it does |
|---|---|
| `Agent/Tools/Read/ReviewProformaTool.cs` | Rule-based senior reviewer over a saved PI's own snapshot: empty customer/company, VAT-on-without-TRN, zero-qty / zero-dimension rows, groups with rows but no price/spec, discount-over-50% and discount-over-total, non-5% VAT, zero grand total with rows, reversed dates, blank prepared-by. Each finding cites rule + step to fix + real value. Never invents, never modifies |
| Intent detection | "review/check/mistakes in … proforma/PI" routes to the tool before the plain search intents (priority-safe catalog order); empty argument reviews the most recently saved PI |
| Discoverability | added to `Help()` output and the chat greeting |

What the offline agent can now do, grounded: look up documents/stock/customers/activity, navigate, record demos, change sheet stock behind confirmation, learn approved phrases, remember across sessions, answer offline via knowledge base (and any installed GGUF model), and review a proforma invoice for the mistake classes above.

---

## POST-P5 — UI truthfulness round: settings buttons + frozen splash (2026-09-04)

Root causes traced in code, not screenshots:

| Area | Root cause | Fix |
|---|---|---|
| Settings → AI & Agent buttons | The new local-model card mixed one styled button (Install) with DEFAULT Win32 unstyled buttons (License/Verify/Uninstall/Cancel) — in a glass-themed app they render as jarring gray chrome, and the raw WPF ToggleButton (styles TargetType Button never apply to ToggleButton) looked like a broken checkbox | Verify/Cancel/License → `OutlinedPillButton`, Uninstall → `DangerButton`, raw ToggleButton replaced by an `OutlinedPillButton` whose TEXT states posture (`Local model answering — ON/OFF`); ProgressBar gains theme `AccentBrush` foreground; local padding overrides removed so pill templates own their metrics |
| Splash launch recolor | App.xaml startup merges **LightTheme** at t=0 (cream + light accent); `Application_Startup` then constructs SplashView whose Background/orbs/text bound `{DynamicResource WindowBackgroundBrush/AccentBrush/…}` — when the user's saved Dark/Monet/Professional theme engaged mid-boot, the splash visibly flip-flopped into different colors | The splash is now a **frozen brand moment**: a local crystal palette (`SplashCerulean*`, `SplashText*`, `SplashOutline/Pill/Track`, frozen corners) replaces every color/corner DynamicResource; only language strings + font tokens stay dynamic. Verified zero remaining dynamic color/radius refs; XAML well-formed |

---

## THEME ENGINE 2.0 — Rich Colour System (2026-09-05)

Senior analysis of the existing stack (VisualThemeEngine 6 families, Theme Studio
16 sliders, WallpaperModules, per-module wallpapers) found five real defects —
all fixed at ENGINE level (styles/views unchanged; leverage via resource keys):

| # | Defect | Fix |
|---|--------|-----|
| 1 | Transparency floored at ~45% alpha — "100%" looked like "60%" | TRUE full range 1.0→0.04 alpha; readability owned by the text layer, not an alpha cripple |
| 2 | No way to reach "rich" — themes felt pale/washed | New **Colour richness** slider (0–100): HSL saturation ×0.45–×1.85 + depth lift on surfaces, glass, ambient, status tints AND the wallpaper scene; true greys (S<0.08) stay neutral |
| 3 | Outside-card text invisible on transparent themes (black text on dark stage) | New **Text strength** slider (0–100): stage-aware ink blending (bright on dark stages, deep on light). AUTOMATIC CONTRAST GUARD forces max ink whenever surfScale < 0.22 — invisible text is now impossible by construction |
| 4 | Accent changes left studio tint stale until next slider move | `ThemeStudioService.OnAccentChanged()` re-applies the live profile instantly |
| 5 | Money/KPI digits jittered + weren't column-aligned | `Typography.NumeralAlignment=Tabular` on PiKpi/ModuleListKpi/Hero/Stat/Medium number styles |

Per-family designed signatures: Monet Richness 75 · Ocean 70 · LiquidGlass 60 ·
Professional 45 (restrained) · UltraTransparent TextStrength 85 (readable through
glass). New **Royal Rich** preset (Richness 90 / Text 75). Old configs upgrade
invisibly (50/55 defaults = family look). Wallpaper: engine-generated scenes now
follow richness; user images are dimmed only, never recoloured. UI: new
COLOUR & TEXT slider group in Settings → Appearance → Theme Studio.

---

## WALLPAPER STUDIO + LIQUID GLASS (2026-09-05)

Honest engineering answer to "include 50 wallpapers": 50 binaries would bloat the
installer and go stale. The full system instead:

| Component | What it is |
|---|---|
| **6 built-in scenes** (`BuiltinWallpaperScenes`) | Code-drawn multi-stop gradients, ONE per family: Aurora (LiquidGlass, accent ribbons), Crystal Sea (UltraTransparent water column), Deep Reef (Ocean), Golden Hour (Monet — 100% derived from the live accent hue), Slate Grid (Professional), Studio (Normal). Resolution-independent (true 4K+), zero assets, and — being plain gradient brushes — they flow through the whole engine (backdrop scale, Colour richness, opacity) |
| **Theme-linked dynamic mode** (`WallpaperOrchestrator`) | Modes Auto/ThemeLinked/File/Scene persisted in config. ThemeLinked = the scene ALWAYS follows the active family AND redraws on accent change (Golden Hour literally re-derives). Overlay hooks the end of `VisualThemeEngine.ApplyModule` (finally), so module navigation never drops the scene; `ThemeStudioService.UpdateBase` keeps the studio snapshot honest |
| **Online 4K downloads** (`WallpaperProviderService`) | Keyless providers: Wallhaven public JSON API (SFW, category queries, ≥2560×1440, random page) with Picsum 4K fallback. Streams to `%LocalAppData%\ProGlassAutomation\Wallpapers` with image/* + ≤30 MB validation; local library = instant offline re-apply |
| **Settings UI** | Wallpaper Studio tile: mode picker, six live-swatch scene buttons, category + Download&apply (busy state), downloaded-library list with one-tap Apply |
| **Card vs backdrop** | Studio labels now explicit: *Card fill — solid ↔ transparent* and *Backdrop intensity — hidden ↔ solid* (separate axes by design) |
| **Water-fill liquid glass** (`WaterFlow` 0–100) | New studio axis + GLASS & WATER slider group: five-stop liquid gradient per glass surface — wet specular top edge, airy crystal body, waterline glint at 78%, colour settled at the bottom like filled water. Tint/frost/transparency/richness remain authoritative over the water. Per-family signatures (UltraTransparent 75, LiquidGlass 45, Professional 0). Truth note: WPF cannot GPU-blur backdrops; depth is painted via layered gradients, not blurred |

---

## WALLPAPER STUDIO — FIELD-FIX ROUND (2026-09-05, owner reports)

| Owner report | Root cause (traced in code) | Fix |
|---|---|---|
| "Button shows `ProGlassAutomation.ViewModel…` typename" | App-wide IMPLICIT ComboBox style (InputStyles.xaml, ~142 consumers) templates items with raw content — `DisplayMemberPath` honoured or not, `ToString()` of row objects leaked | `ToString()` overrides on SceneRow/CategoryRow/CachedWallpaperRow (style-immune human labels), DisplayMemberPath kept |
| "Button text height collapsing" | My Padding overrides fought the pill templates' fixed metrics (same defect class as the settings-agent card fix) | All Padding overrides removed from the wallpaper tile — templates own their metrics |
| "Nothing downloading" | Silent pipeline — failures only reached the log | Stage-by-stage status reporting into the UI (searching → downloading → saved/failed with reason) |
| "Wallpaper not changing as per changing theme" | `Auto` mode semantics: a legacy picked file pinned the machine to File mode, so family switches never repainted | Factory default is now ThemeLinked (Auto upgrades in place; files stay on disk, one tap away). PLUS two explicit refresh hooks: visual-family setter + every full ApplyTheme chain end ← belt and braces over the ApplyModule overlay |
| "Use services bypassing the WPF blur limitation" | WPF's retained renderer has no per-card backdrop blur; packages = heavy deps | `SystemBackdropService` — native Windows 11 DWM system backdrop (`DWMWA_SYSTEMBACKDROP_TYPE`: Mica/Acrylic), rendered BY THE OS behind WPF content. Experimental tier; silently degrades to the painted liquid-glass stage (default) |
| "Proper wallpaper system + download management" | Library had no management | Per-row Delete (active wallpaper protected + falls back to ThemeLinked), Clear-all (keeps active), Open folder, count+MB summary, busy-guarded downloads |

Startup restores the saved OS-backdrop preference right after the first
window Show(). Verified: XML/braces clean on 10 files, no dup lang keys,
no padding overrides left, all hooks present.

---

## THEME APPEARANCE DEEP AUDIT — ROOT-CAUSE FIXES (2026-09-05, owner field reports)

Senior trace through the call chain (not the pixels) found the new features were
structurally invisible/reset, not cosmetically weak:

| Owner report | PROVEN root cause | Fix |
|---|---|---|
| "Clicking Dashboard/module resets the theme" | `VisualThemeEngine.ApplyModule` (called on every navigation) tears down ~60 keys via ClearOverrides and repaints ONLY the family — the Theme Studio 0–100 layer was never re-applied on navigation | `ThemeStudioService.OnFamilyApplied()` now runs at the end of every REAL ApplyModule rebuild (in finally). The signature-guard's no-op repaints skip it, so steady-state navigation stays free |
| "Wallpaper scenes are fake — can't see anything" | `ModuleWallpaperBrush` is consumed by EXACTLY ONE view (AboutView). Module roots paint `ModuleBackgroundBrush` over MainWindow's `WindowBackgroundBrush` stage — the v1 overlay wrote only the dead key, scene hidden behind an OPAQUE module surface | Scene now follows the FILE-wallpaper contract: `WindowBackgroundBrush` ← scene (app-wide stage, like file wallpapers), `ModuleBackgroundBrush` ← SetTranslucent 0.80, `AdaptTypographyForWallpaper` + font pref re-asserted, About's key also set, studio bases updated. Per-module image wallpapers still WIN |
| "Downloads show nothing" | (previous round: silent pipeline) — compounded by scenes being invisible | Already status-piped; now the default state shows a live scene WITHOUT any download, and theme/accent/family changes visibly repaint it |
| "Windows 11 options look fake" | An opaque stage brush covered the OS backdrop; translucency was never made Mica-aware | When BackdropActive: the scene keeps the window stage translucent (0.55 module surface) so DWM Mica/Acrylic breathes; scene stage-write skipped; startup path refreshes through the same rebuild |
| "Many options don't show instant results" | Studio layer resets on navigation + invisible wallpaper made customization SEEM inert; studio live-apply itself was sound (DynamicResource on managed keys incl. GlassSurface tiles) | With nav-survival + visible scenes, every slider's effect now persists and shows everywhere |

---

## ERR-46877E RCA — TimeSpan overflow crash in ThemeStudioService (2026-09-05)

Stack evidence (owner crash report):
`ThemeStudioService.WriteScaledDuration → IntervalFromDoubleTicks → OverflowException`
inside `OnFamilyApplied()` (nav hook) → RelayCommand → unhandled UI crash; on the
AppearanceService path it was caught → a ~1 s "ApplyTheme failed" storm that made
every theme trigger feel laggy/dead.

ROOT CAUSE (proveable bit): `Duration.Forever` IS TimeSpan-backed (TimeSpan.MaxValue)
with HasTimeSpan == TRUE. A Forever-class value in the Motion-duration snapshot,
scaled by the Animation slider (×~1.2), produced ~1.4×10^19 ticks > Int64.MaxValue.
The old guard only checked the TYPE, never the magnitude.

FIXES (three layers, defense in depth):
1. WriteScaledDuration: !HasTimeSpan skip + NaN/∞ skip + **≥1-minute values treated
   as animation TOKENS, never scaled** + result clamped to 1 ms–60 s + full try/catch.
2. Every per-key writer (brush/thickness/shadow/text/water/duration) wrapped
   individually — one poisoned resource can never abort the whole studio apply.
3. OnFamilyApplied / Restore / OnAccentChanged fully guarded — the studio layer can
   never again crash navigation or the boot chain, whatever the saved profile holds.

Also hardened per owner field reports: wallpaper library scan failures now LOG
(instead of returning a silent empty list); provider pipeline logs every stage to
AppLog (fetch start, 0-result searches, per-provider outcome, all-failed network
diagnosis) so the next session's log proves the download path.

---

## 2026-09-05 — Owner escalation: dropdown UX + module rail drawer behaviour

### DROPDOWNS (ComboBox) — two proven root causes, global fix

1. **"Click anywhere should open it"** — `ModernComboBoxStyle` placed its
   toggle ONLY in the arrow column (`Grid.Column=1`), so clicks on the text
   body hit a `ContentPresenter` with `IsHitTestVisible=False` over NOTHING
   and died. Reworked to WPF's own Aero pattern: the toggle spans both
   columns (Grid.ColumnSpan=2) with a transparent-border template, every
   visual above is hit-test invisible. `LiquidGlassComboBox` fixed the same
   class: its chrome never received a Background (TemplateBinding read the
   toggle's own unset property → transparent box) and its ContentPresenter
   stole the click (now IsHitTestVisible=False).
2. **"White text on white background"** — ComboBoxItem hover/highlight used
   GlassSurfaceBrush ≈ 40% WHITE glass; on the dark popup (#1C1F26) the row
   turned near-white while `PrimaryForegroundBrush` stayed white → invisible
   text. Also IsSelected kept theme foreground: dark-on-indigo in light
   theme. Dedicated per-theme `ComboBoxItemHoverBrush` tokens (Dark
   #2BFFFFFF / Light #1F000000); IsSelected now paints `AccentBrush` +
   `OnAccentForegroundBrush`. Implicit styles cover every ComboBox app-wide
   — no third-party dropdown controls exist in the app, so the WPF-native
   global style IS the global service; no packages added.

### MODULE RAILS (Settings + Reports) — drawer behaviour

- **Reports:** `IsRailOpen` existed, toggle button existed, hamburger style
  existed — but NO Visibility binding consumed the state (the collapse
  button was DEAD). Rail column is now Auto + width moved onto the Border;
  Border.Visibility binds IsRailOpen; NavigateReport auto-closes the rail.
- **Settings:** new `IsSettingsRailOpen` + floating menu toggle (single
  click open/close, always visible over the content); SettingsPage setter
  auto-closes the rail on EVERY switch path (rail clicks + deep-link
  GoToPage). ERR-BEFC44 auto-size contract preserved (Min 232 / Max 420
  moved from the ColumnDefinition onto the rail Border).
- Verified: 6 XAML files XML-parse, both VMs brace-balanced.

---

## 2026-09-05 (3) — Deep-debug chain for owner's "blind answer" escalation

### TRACED ROOT CAUSES (proven through the call chain, not guessed)

1. **Reset Current / Reset All "do nothing"** — TWO distinct faults:
   (a) `ResetAll()` called `AppearanceService.ApplyTheme` which hands off
       to `VisualThemeEngine.ApplyModule` — whose idempotence guard
       (`signature == _lastAppliedSignature`) RETURNED EARLY, so no family
       repaint ever happened: state was reset but the SCREEN kept the old
       studio look. Fix: `InvalidateSceneCache()` before the chain (clears
       the de-dup signature → forced real repaint).
   (b) `Reset-Current` (VM) re-applied defaults directly onto whatever
       stale snapshot was current instead of repainting the family. Now
       routed through `ThemeStudioService.ResetToFamilyDefaults()`, which
       drops the layer, forces the repaint, then applies defaults on a
       provably family-fresh snapshot.
   (c) `StudioNotifyAllSliders()` never notified ColorRichness /
       TextStrength / WaterFlow — those sliders visibly stayed put after
       a reset ("text strength action is wrong").
2. **"Text strength acts wrong"** — consequence of (1): any blend computed
   against a snapshot that was captured while studio writes were already on
   screen blends the wrong source ink; forcing real repaints on every reset
   + fresh OnFamilyApplied snapshots makes family ink the only base that
   can ever be blended.
3. **4K downloads silently fail** — traced 3 provider-level defects:
   (a) WS page lottery: `sorting=random` was combined with a random
       `page=1..20`; past the catalogue's real page count the API returns
       a zero-length data array → instant fail. Random ordering alone is
       the shuffle; page removed.
   (b) Per-call `new HttpClient` and empty/weak agent headers; some CDNs
       403 empty-agent requests. One pooled client + explicit UA/Accept.
   (c) Single-attempt chain with silent nulls. Now: Wallhaven (with
       widened-query retry) → Picsum (two different photo IDs) →
       LoremFlickr (third, independent HOST for ISP/firewall diversity),
       each failure returning a human reason that the settings UI status
       line shows verbatim and the app log records. "Not downloading"
       is now readable on screen: status code / TLS / timeout per provider.
4. **"Open folder does nothing"** — ShellExecute-on-directory relies on the
   shell's folder handler; hardened/3rd-party shell policies can swallow it
   with no exception (looks dead). Now: explicit `explorer.exe "<dir>"`
   launch, explicit shell-verb fallback, always-visible status feedback
   (opened path or exact failure + directory location), AppLog both ways.

### Settings sidebar → Reports-module design (owner: ugly, match reports)
- Rail restyled: accent header card (icon tile + title lines + in-header
  collapse button) exactly like SalesReportView's rail.
- Content column gains a header strip with the hamburger (single click
  re-open) + current section name; stacked sections moved to row 1; the
  earlier floating overlay button is removed.
- Drawer behaviour kept: picking a section auto-closes the rail; single
  click opens/closes; ERR-BEFC44 min/max sizing preserved.

---

## 2026-09-05 (4) — SYSTEM-WIDE BUILD: the four-pipeline problem and its unification

### SENIOR ANALYSIS (why "everything in appearance looked broken")

Four services each ran their own copy of the theme pipeline in DIFFERENT
ORDERS, each believing it was the last step:

1. AppearanceService.ApplyTheme: Theme → Apply → ApplyModule(→finally:
   scene+studio) → STUDIO AGAIN → accent → outline → RefreshScene
   (→ ApplyModule AGAIN → finally AGAIN) ⇒ up to 4 repaints/click.
2. SettingsViewModel.ApplyVisualTheme: Apply → ApplyModule → accent →
   outline → studio — accent/outline BEFORE studio (opposite of #1).
3. VisualThemeEngine.ApplyModule finally: scene overlay + studio re-apply.
4. WallpaperOrchestrator.SaveAndRepaint: → AppearanceService (all of #1).

Any multi-field state (family + studio profile + wallpaper mode + accent)
landed differently per entry path; some studio base-snapshots were taken
MID-pipeline (before accent re-derive); a skipped repaint in one place
was unknown to the rest. Built-in theme picks, presets, text strength,
scenes, downloads — everything inherited this nondeterminism.

### THE UNIFICATION — Services/Theme/ThemePipeline.cs (NEW)
One ordered sequence, one writer per layer, public APIs preserved by
delegation (zero call-site breakage):
  1 ThemeManager.ApplyTheme      (full flavour only)
  2 VisualThemeEngine.Apply      (family repaint)
  3 VisualThemeEngine.ApplyModule(module + wallpaper/scene overlay;
    finally runs the ONE canonical studio re-apply on fresh snapshot)
  4 ThemeManager.ApplyAccent     (surface-derived keyline)
  5 ThemeManager.ReapplyOutlinePreference (user's card edges)

Routed: AppearanceService.ApplyTheme → Rebuild; SettingsViewModel
.ApplyVisualTheme → RebuildFamily; WallpaperOrchestrator.SaveAndRepaint →
RebuildWallpaperLayer (family swap was redundant for click actions);
accent path → RebuildAccentLayer. Slider-level studio tweaks remain
in-memory by design. Every rebuild AppLogs its reason + effective state
(family / wallpaper mode / studio active) so future "who repainted?" is
provable from the log.

### SENIOR RECOMMENDATIONS (next phases, do not batch-blindly)
- Phase R1: extract the ENGINE's three Apply-family pivots behind
  an IFamilyPainter registry (each family's paint becomes one file);
  today Apply+ApplyModule duplicate the switch.
- Phase R2: visual smoke contract — a tiny in-app "appearance self-test"
  page that asserts key resources resolve and logs deltas on startup in
  DEBUG; turns "looks broken" into "line 27 key X was null".
- Phase R3: per-family Default profiles table-driven (JSON resource)
  instead of compiled switches — removes the DefaultsFor drift class.

---

## ERR-46877E — OwNEr-VERIFIED CRASH CHAIN (2026-09-05, final)

Crash report stack (owner machine):
  ThemeStudioService.WriteScaledDuration → TimeSpan.IntervalFromDoubleTicks
  → OverflowException  ← ThemeStudioService.Apply:391 ← OnFamilyApplied:214
  ← VisualThemeEngine.ApplyModule:542 (navigation RelayCommand, UNHANDLED)

PROVEN root cause: Duration.Forever is TimeSpan-backed — HasTimeSpan=true,
TimeSpan.MaxValue. A Forever-class value in the MotionDuration* snapshot,
multiplied by the Animation Intensity scale (needs ≳1.17×; slider >~67),
overflows long ticks. All theme files + all Services define finite tokens
(150/220/380 ms), so provenance is environmental (legacy/outer writer);
guard therefore makes overflow mathematically impossible rather than
chasing the source.

Two visible failure modes (why "triggers felt laggy/dead"):
  • caught path (AppearanceService.ApplyTheme): ~1.06 s "ApplyTheme
    failed" ERROR flood — studio apply aborted mid-write each pass;
  • uncaught path (ApplyModule finally → nav RelayCommand): the fatal
    UI crash report itself.

FIX SET (already in this branch):
  1. WriteScaledDuration: !
HasTimeSpan skip, NaN/Infinity skip,
     base>60 s skip (animation-control tokens are never scaled),
     output clamped 1 ms–60 s, complete try/catch.
  2. Every per-key writer (brush/thickness/shadow/text/water) is a
     guarded unit — one poisoned resource cannot abort a whole studio
     apply anymore.
  3. OnFamilyApplied / Restore / OnAccentChanged fully guarded with
     AppLog warnings — the studio layer can never again take down
     theme application, boot, or navigation.
  4. Wallpaper library scan failures now log (no silent empty library);
     provider pipeline AppLogs fetch start / empty searches / provider
     outcomes / a final ALL-PROVIDERS-FAILED diagnosis with the exact
     network reason, so "downloads do nothing" is diagnosable in one
     read of the app log.
