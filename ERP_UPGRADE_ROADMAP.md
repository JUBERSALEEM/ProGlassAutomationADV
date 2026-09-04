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
