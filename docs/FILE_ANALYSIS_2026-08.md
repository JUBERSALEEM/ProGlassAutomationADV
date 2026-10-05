# Full-tree analysis — 2026-08-18

Inventory of every source file in this checkout, plus what is still
open after the test fix and Phase 9 header/wizard rewire.

Static analysis only. This sandbox has no .NET SDK / no Windows, so
`dotnet test` and the Release build remain yours (Phases 17 / 18).

---

## 1. Inventory

| Kind | Count |
|---|---|
| Tracked files (git) | 885 |
| C# (`.cs`) | **578** |
| XAML | 139 |
| Markdown | 76 |
| Tests (C#) | 27 files / 459 cases last run |
| Views XAML | 113 |

The "569 files" figure from the last Windows run is the C# set as it
stood then (578 now, plus the files added in later phases). Every
`.cs` file was opened by script for size, TODO/placeholder, and
empty-catch signals; every `.xaml` file was measured for hardcoded
hex, radii, and shared-primitive adoption.

### C# lines by area

| Area | Files | Lines |
|---|---|---|
| ViewModels | 121 | 41,527 |
| Services | 150 | 34,562 |
| Models | 79 | 9,206 |
| Agent | 49 | 8,208 |
| Views (code-behind) | 109 | 6,391 |
| Tests | 27 | 5,008 |
| Everything else | 43 | ~7,700 |
| **Total** | **578** | **112,579** |

Largest product surface: `ViewModels/ProformaInvoice` (38 files,
14,930 lines). That is the 9-step wizard plus the saved-invoice list.

---

## 2. Test failure that blocked Phase 17 — FIXED

Windows run: **458 passed, 1 failed**.

`TextParsing.CleanNum("")` documented "empty/null collapses to `0`"
but only the `?? "0"` null-coalesce fired. `""` stayed `""`, so
`CleanNum_strips_separators_and_whitespace(input: "", expected: "0")`
failed.

Fix in `Utilities/TextParsing.cs`: empty / whitespace / null all
return `"0"`. Extra theory case `"   "` added. Call sites already
feed the result into `int.TryParse`, so `"0"` is the safe contract.

Analyzer warnings from the same run, also fixed:

| Rule | File | Fix |
|---|---|---|
| xUnit1012 | `TaxInvoiceDnReconcilerTests.Empty_reference_returns_empty` | parameter is now `string?` so `[InlineData(null)]` is legal |
| xUnit2008 | `SpecParsingTests.Hybrid_spec_matches_both_spacer_and_interlayer` | `Assert.True(regex.IsMatch(...))` → `Assert.Matches` |

Re-run on Windows should be **459 passed, 0 failed** (one extra
theory case). The 566 compiler warnings are pre-existing (nullable
annotations, unused fields, WPF generated code) and were not in
scope of this increment.

---

## 3. God-ViewModels still open (Phase 11/12)

These are the remaining splits. Do **not** start them in the same
commit as UI chrome — they are behaviour-risk.

| Lines | File | Notes |
|---|---|---|
| 2119 | `TaxInvoiceDetailViewModel` | next extraction target |
| 1951 | `StatementGeneratorViewModel` | wizard + PDF pipeline |
| 1910 | `DeliveryNoteDetailViewModel` | next extraction target |
| 1702 | `SettingsViewModel` | next extraction target |
| 1701 | `StatementWorkflowEngine` | service, not a VM |
| 1588 | `ProfitLossStepViewModel` | already has a test suite |
| 1581 | `SpecGroup` | P&L grouping |
| 1565 | `PrintableInvoiceViewModel` | print surface |
| 1053 | `SavedInvoicesListViewModel` | |
| 1032 | `DeliveryNoteListViewModel` | |

Five extractions already landed (`b49c02a`, `8abd438`, `a4943c0`,
`34e1343`, plus earlier PI splits). The three named in the phase
board (DN Detail, TI Detail, Settings) are still the next real
engineering work after this UI increment.

---

## 4. Phase 9 — what this increment did

Design-system primitives were defined but almost unused:

| Primitive (before) | Views using it |
|---|---|
| `ModulePageTitleStyle` | 4 / 113 |
| `ModuleSectionCardStyle` | 0 / 113 |
| `ModuleToolbar*` | 0 / 113 |

Added to `Themes/Styles/DesignTokens.xaml`:

- `ModuleDetailTitleStyle` — compact document-id title (DN/TI action bar)
- `ModuleDetailActionBarStyle` — the one detail chrome card
- `ModuleWizardStepIconStyle` — 44×44 accent tile
- `ModuleErrorBannerStyle` / `ModuleSuccessBannerStyle`
- `ModuleFieldLabelStyle` / `ModuleInlineWarningStyle`

Rewired onto those primitives (bindings / commands untouched):

- JO / DN / TI detail headers and error banners
- JO / DN / TI list error banners
- PI list page title
- PI wizard steps 1, 2, 3, 5, 6, 7, 8 headers
- Steps 1–3 field labels, inline warnings, error/success/hover tints

Leftover **executable** indigo removed from:

- `App.xaml` implicit TextBox focus ring (`#4338CA` / `#6366F1`)
- `Styles/Step4SectionStyles.xaml` input/combo/chevron

`#4338CA` that remains is comments, PDF exporters (print ink, not
theme), and a few VM chart/status hex strings. Those are a later
pass — changing PDF ink without a printed-proof is how you ship a
wrong invoice.

### Still the worst visual debt (do next, one module per commit)

| File | Why |
|---|---|
| Statement `FilterStep` / `PreviewStep` / `ExecuteStep` | 40–58 hardcoded hex each |
| `PrintableInvoiceView.xaml` | print surface, 41 hex — leave until a print proof |
| `SplashView` / `LoginWindow` | intentional dark-brand surfaces (already redesigned) |
| DN / TI list row semantic hex (`#047857`, `#B91C1C`) | status colour, should become Success/Danger brushes |
| `WizardStyles.xaml` purple gradient stops (`#8B5CF6`, `#7C3AED`) | statement wizard chrome |

---

## 5. Module-by-module C# read (all 578)

Verdict key: **OK** = no action this increment · **OPEN** = real
follow-up · **PLACEHOLDER** = ComingSoon shell.

### Application shell — OK
`App.xaml.cs` (832), `MainWindow.xaml.cs` (443), `AssemblyInfo.cs`.
Theme swap, startup, lockout, forced-password routing live here.
Focus ring now follows `AccentColor`.

### ViewModels/ProformaInvoice (38 / 14,930) — OPEN chrome done, VMs large
Wizard is complete (steps 1–9 + saved list). Largest remaining
classes are P&L (`ProfitLossStepViewModel`, `SpecGroup`) and the
printable invoice VM. Headers now share the design-system title /
icon. Do not restyle `PrintableInvoiceView` without a print proof.

### ViewModels/DeliveryNote (10 / 4,719) — OPEN split
`DeliveryNoteDetailViewModel` (1910) and `DeliveryNoteListViewModel`
(1032) are the Phase 11/12 targets. Header chrome is now shared.

### ViewModels/TaxInvoice (9 / 4,249) — OPEN split
`TaxInvoiceDetailViewModel` (2119) is the single largest file in the
repo. Header chrome is now shared.

### ViewModels/JobOrder (5 / 2,326) — OK
Detail VM is 794 lines (already extracted). List is 897. Fine.

### ViewModels/Settings (2 / 1,817) — OPEN split
`SettingsViewModel` (1702) is the third Phase 11/12 target.

### ViewModels/Reports (9 / 3,503) — OPEN visual
`StatementGeneratorViewModel` (1951) plus the Filter/Preview/Execute
XAML are the worst remaining hardcoded-colour cluster.

### ViewModels/Dashboard (11 / 3,018) — OK
Reference implementation. Theme brushes, `StatCardControl`,
`EmptyStateControl`. One leftover chart hex `#4338CA` in
`DnDashboardViewModel` (bar colour string).

### ViewModels/SheetStore (7 / 1,587) — OK
Pilot-migrated. `SheetStoreViewModel` is 997 lines but cohesive.

### ViewModels/AI, About, Company/User/System, Diagnostics, Shared — OK
No Phase 9 header work needed. `AiChatViewModel` is 919 lines.

### ViewModels/Production (8 / 88) + Optimization (1 / 11) — PLACEHOLDER
Every file is a ComingSoon shell. Phase 20 cannot go Production Ready
until these are built or the nav stops presenting them as modules.

### Services/Statement (12 / 4,966) — OK / PDF indigo leftover
`StatementWorkflowEngine` (1701) is large but already extracted from
the VM. PDF accent `#4338CA` is print ink.

### Services/TaxInvoice, DeliveryNote, ProformaInvoice, JobOrder — OK
Repositories, mappers, PDF/Excel exporters. Same print-ink leftover
in TI/Statement PDF documents.

### Services/AI (23 / 4,892) — OK
Provider fallback, context builder, tools. Covered by Agent tests.

### Services/Security, Updates, Database, Audit, Theme — OK
Lockout + forced password + forgot-password already applied
(Phase 13). Update origin pinning has tests.

### Agent/* (49 / 8,208) — OK
Tools, permissions, planning, voice. 27 test files pin the
boundaries (permissions, write tools, offline, provenance).

### Models/* (79 / 9,206) — OK
Status machines, list items, workflow records. A few `#4338CA`
defaults on statement workflow status colours.

### Controls, Behaviors, Converters, Utilities — OK
`TextParsing.CleanNum` contract restored. `DataGridNavigationBehavior`
(964) is dense but isolated. Converters' `NotImplementedException` on
`ConvertBack` are the normal WPF one-way pattern, not TODOs.

### Tests/Agent (27 / 5,008) — OK after this fix
Coverage is calculations, parsing, security, permissions, P&L, SQM,
status transitions, statement snapshot, DN reconciler. **No UI tests
and no VM integration tests** — expected for a WPF app in this repo.
Phase 17 is the Windows `dotnet test` confirmation.

### Views/Production + Optimization XAML — PLACEHOLDER
Eight production views + Optimization are `<shared:ComingSoonView/>`.

---

## 6. What is *not* left for me in this sandbox

| Phase | Status |
|---|---|
| 17 `dotnet test` | yours — Windows |
| 18 Release build | yours — Windows |
| 20 Production Ready verdict | blocked on 17/18 + real Production/Optimization modules |
| 11/12 God-VM splits | real work, next engineering increment |
| Statement wizard hex | next UI increment, one wizard, one commit |
| PDF indigo | print-proof required |

---

## 7. Recommended next commit (after you confirm 459 green)

1. Extract `DeliveryNoteDetailViewModel` (1910) — highest-traffic
   remaining God VM after TI Detail.
2. Or, if staying on UI: Statement `WizardStyles.xaml` purple
   gradient stops → theme accent, then Filter/Preview/Execute
   one file at a time.

Do not combine a God-VM split with a visual pass.
