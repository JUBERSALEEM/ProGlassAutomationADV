# 🗺️ ProGlass Automation — Phased Roadmap to a Professional WPF Platform

**Status snapshot (2026-08-14)** · `v2.0.0` · branch `arena/019fff44-proglassautomationadv`

| Metric | Value |
|---|---|
| C# files | 381 |
| XAML files | 96 |
| Test project | ❌ none yet |
| CI pipeline | ❌ none yet |
| DI container | ❌ manual/static wiring |
| Placeholder modules | ✂️ Optimization only |
| Themes shipped | Normal ✅ · Monet ✅ (refine) · Professional ✅ (refine) |

The V1 → V2 core upgrade (identity, audit, versioning, design system, theming,
backup sets, updates, AI assistant) is **delivered**. The phases below take the
application from *"upgraded V1"* to *"shippable professional product"*.

**Golden rules for every phase:**
1. Additive only — nothing working is removed.
2. One module / one concern per commit; no big-bang rewrites.
3. Every phase ends with an acceptance checklist, not "looks fine".

---

## Phase 1 — On-Device Verification & Stabilization ⚓ (current)

> The sandbox cannot compile `net8.0-windows`; this phase happens in Visual
> Studio on the user's machine, with fixes fed back here.

**Goal:** prove the delivered V2 upgrade on real hardware and reach a zero-error baseline.

| Task | Detail |
|---|---|
| 🏗️ Clean rebuild | VS 2022 / .NET 8 SDK, `net8.0-windows`, warning review |
| 🚀 Startup smoke | Splash → main shell, all 16 view areas open without exceptions |
| 🔎 Binding sweep | Output window: no binding errors, no missing resources, no MC codes |
| 🎨 Theme smoke | Switch Normal → Monet → Professional; verify popups, dialogs, God Views |
| 🧾 Document smoke | One full PI wizard pass (Step1→8) → JO → DN → TI chain |
| 💾 Safety smoke | Backup set creation, restore with safety snapshot, version-history restore |

**Exit criteria:** zero compile errors · zero binding/resource errors · full document chain works · logs clean.

---

## Phase 2 — Module-by-Module Interior Upgrade (pilot-first) 🧩

> Chrome is themed everywhere; module *interiors* still migrate one at a time.

**Goal:** every module reaches the V2 interior standard.

**V2 interior checklist (per module):**
- [ ] All solid colors → theme brushes / `DesignTokens`
- [ ] Accent table headers + pill action buttons (PI-list reference)
- [ ] `EmptyState` for empty grids, `StatCard` where numbers matter
- [ ] Loading / updating states on async operations
- [ ] Keyboard: Tab order, Enter/Escape in dialogs, shortcuts for primary actions
- [ ] Virtualization on large grids; background work off the UI thread
- [ ] Unsaved-changes guards on every dirty form
- [ ] Permissions enforced by the operation (not only hidden buttons)

**Order (pilot-first, highest traffic first):**
1. 📦 Sheet Store (pilot — smallest surface, proves the checklist)
2. 📈 Sales Report · 📑 Statement Generator
3. 🏭 Job Order interior → 🚚 Delivery Note → 🧮 Tax Invoice
4. 📊 Dashboard (stat cards + sparklines on real data)
5. ⚙️ Settings refinements

**Exit criteria:** checklist ✅ for all modules above; no solid-color literals outside `Themes/`.

---

## Phase 3 — Theme Completion: Monet & Professional 🎨

**Goal:** the two showcase themes stop being "tinted Normal" and become genuinely distinct.

| Theme | Work |
|---|---|
| 🌸 Monet | Transparency tiers per surface, wallpaper-aware contrast checks, soft shadows, gentle parallax where cheap |
| 💎 Professional | Higher contrast hierarchy, denser spacing option, restrained motion, business-report look |
| 🖼️ Wallpapers | Curated default wallpaper per module; opacity presets per theme |
| ⚡ Motion | Verify animation scaling on 120/144/165 Hz; no dropped frames in God Views |

**Exit criteria:** side-by-side visual review of all 16 view areas × 3 themes passes; wallpapers readable at min/max opacity.

---

## Phase 4 — Optimization Module Rebuild ✂️

> Currently the only placeholder (`OptimizationView` only). Highest business value left.

**Goal:** real cutting optimization wired into the document pipeline.

| Task | Detail |
|---|---|
| 🧠 Engine | Guillotine/strip cutting: input pieces (from PI/JO glass data) + stock sheets from Sheet Store |
| 📉 Waste report | Utilization %, offcuts returned to Sheet Store inventory |
| 🖼️ Cut diagram | Themed vector diagram with piece labels, printable via shared PDF stack |
| 🔗 Integration | PI Step5 grid → "Send to Optimization"; result → JO production reference |
| 🕐 Traceability | Plans versioned + audited like every other document |

**Exit criteria:** a PI can generate a cutting plan that consumes real stock and posts offcuts back.

---

## Phase 5 — Quality Infrastructure 🧪

> No tests and no CI today. This phase makes future phases safe.

| Task | Detail |
|---|---|
| 📦 `ProGlass.Tests` (xUnit) | Pricing/SQM/LM math, permission matrix, audit diff engine, backup rotation, version-history restore-as-new, update-history dedupe |
| 🏭 CI workflow | GitHub Actions Windows runner: `dotnet build` + `dotnet test` on PR |
| 🧹 Hygiene | Warning-as-error ratchet; XAML lint via build |
| 📈 Baseline | Core calculation services ≥ 80% coverage before Phase 6 |

**Exit criteria:** PR builds + tests green in CI; pricing and audit engines covered.

---

## Phase 6 — Release Engineering 📦

| Task | Detail |
|---|---|
| 🛠️ Installer | WiX/MSIX: binaries → Program Files, **data stays in `%AppData%/ProGlassAutomation/`** |
| 🔄 Update pipeline | `UpdateService` check → release notes → guided download; app never self-replaces |
| ✍️ Signing | Code-sign the executable + installer (SmartScreen trust) |
| 🧊 Upgrade path | V1 data detected → migrated in place, backup first, rollback documented |
| 📋 Release artifacts | Versioned release notes generated from `UpdateHistoryService` data |

**Exit criteria:** clean install → upgrade → uninstall cycle on a fresh Windows machine; user data intact and untouched by uninstall.

---

## Phase 7 — Professional Polish / V3 Candidates 🌟

Ordered by likely value — pick per feedback after Phase 6:

- 🌍 **Localization** — English + Arabic (RTL-aware layout pass); resource-driven strings
- ♿ **Accessibility** — full keyboard operation, narrator labels, high-contrast checks
- 🧱 **DI retrofit** — `Microsoft.Extensions.DependencyInjection` for services (architecture cleanup, additive)
- 🖨️ **Print pipeline v2** — branded headers/footers, batch printing for DN/TI runs
- 📊 **Report enrichment** — customer aging, glass-type mix, waste trends on Dashboard sparklines
- 🔔 **Operational alerts** — backup-age warnings, stock low-water, duplicate-document detection surfaced as notifications

---

## Sequence & Ownership

```
Phase 1 ⚓ verify+stabilize   → user rebuilds, agent fixes reported errors
Phase 2 🧩 module interiors   → agent-led, one module per batch, user reviews
Phase 3 🎨 theme completion   → agent-led, user picks the winner per screen
Phase 4 ✂️ optimization       → agent-led, business rules confirmed with user
Phase 5 🧪 tests + CI         → agent-led
Phase 6 📦 installer/release  → agent drafts, user signs & distributes
Phase 7 🌟 polish / V3        → prioritized together
```

**Dependency notes:** Phase 2–4 can interleave, but each module ships only
after Phase 1 clears it. Phase 5 should land before Phase 6 so release builds
are gated by tests. Phase 7 items are independent and orderable per feedback.
