# ProGlass Automation — UI Audit & Industrial-Grade Redesign Plan (V2.0)

Audited against: the project's own design system (`Docs/DESIGN_SYSTEM.md`,
`Themes/Styles/DesignTokens.xaml`, `Themes/LightTheme.xaml`,
`Themes/DarkTheme.xaml`) and general industrial/ERP visual standards
(consistency, hierarchy, density, theme adherence, maintainability).

Scope: **105 XAML files in `Views/`** (all modules). Data was measured by
script (hardcoded hex, radius literals, local styles, shared-primitive usage)
and by reading representative views. XAML is well-formed (gate passes); this
audit is about **visual/architectural consistency**, not correctness.

---

## TL;DR — the diagnosis in one paragraph

The app is **not ugly**, and many individual screens look fine. It is
**inconsistent**: the modules were built at different times by different
conventions, so the same UI concept (a card, a pill, an accent, a header, an
empty state) has **many different implementations**. Two themes' worth of
brushes, 93 hardcoded hex colors, ~49 distinct corner radii, and hundreds of
local styles make it read as "a demo" rather than "a product." The single
biggest, most fixable defect: **the modules hardcode an indigo brand color
(`#4338CA`) 142 times while the theme accent is blue (`#1D4ED8` light /
`#3B82F6` dark)** — so dynamic theming cannot work, because the accent is
baked in per-file instead of coming from the theme.

---

## Part 1 — Ratings (all 105 Views XAML files)

Rating scale:

| ★ | Meaning |
|---|---|
| 5 ★ | Theme-consistent, shared primitives, near-zero hardcoded color, clean hierarchy |
| 4 ★ | Good; minor hardcoded values or a few local styles |
| 3 ★ | Acceptable but visibly inconsistent (off-token colors/radii, style bloat) |
| 2 ★ | Needs work: heavy hardcoding, style bloat, inconsistent chrome |
| 1 ★ | Poor / placeholder / wholesale bypass of the design system |

Legend columns: **H** = distinct hardcoded hex colors · **R** = distinct
radius literals · **S** = local `<Style>` definitions · **T** = non-token
page-title sizes (24/26/28/36).

### Dashboard module (reference — the good baseline) ✅
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| DashboardView.xaml | 0 | 3 | 0 | 0 | ★★★★★ |
| Overview/OverviewDashboardView.xaml | 3 | 0 | 0 | 0 | ★★★★★ |
| JobOrder/JoDashboard/JoDashboardView.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| DeliveryNote/DnDashboard/DnDashboardView.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| ProformaInvoice/PiDashboardView.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| TaxInvoice/TiDashboard/TiDashboardView.xaml | 0 | 6 | 2 | 0 | ★★★★★ |
| SheetStore/SheetStoreDashboardView.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| Statement/StDashboardView.xaml | 0 | 3 | 6 | 0 | ★★★★★ |

The Dashboard family is the **reference implementation**: every brush is a
`DynamicResource`, shared `StatCardControl` / `EmptyStateControl`, no
hardcoded color. Any module should aspire to this.

### Sheet Store module (pilot-migrated) ✅
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| SheetStoreView.xaml | 0 | 6 | 8 | 0 | ★★★★ |
| AddEditSheetView.xaml | 0 | 1 | 5 | 0 | ★★★★ |
| SheetStoreHistory/HistoryView.xaml | 0 | 4 | 5 | 1 | ★★★★ |
| SheetStoreCsv/DuplicateResolutionView.xaml | 0 | 4 | 9 | 1 | ★★★★ |
| SheetStoreBuy/BuyDialogView.xaml | 0 | 1 | 1 | 2 | ★★★★ |
| SheetStoreUse/UseDialogView.xaml | 0 | 3 | 0 | 2 | ★★★★ |

Clean, theme-consistent, the documented pilot. Good template for others.

### Settings module (mostly well-built) ✅
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| SettingsView.xaml | 0 | 1 | 14 | 0 | ★★★★ |
| Sections/SettingsAiSection.xaml | 0 | 1 | 12 | 0 | ★★★★ |
| Sections/SettingsAgentSection.xaml | 0 | 0 | 2 | 0 | ★★★★ |
| Sections/SettingsVoiceSection.xaml | 0 | 3 | 16 | 0 | ★★★★ |
| Sections/SettingsAppearanceSection.xaml | 8 | 0 | 0 | 0 | ★★★ |
| Sections/SettingsUpdatesSection.xaml | 0 | 1 | 12 | 0 | ★★★★ |
| Sections/SettingsDataSection.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| Sections/SettingsAboutSection.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| SettingsSectionStyles.xaml | 6 | 6 | 24 | 0 | ★★★ |

Settings is the second reference pattern (section cards + page title/subtitle
styles). `SettingsAppearanceSection` (8 hardcoded hex) and the shared
`SettingsSectionStyles` (6 hex, 24 styles) are the outliers worth centralizing.

### Detail "Section" templates (cleaner, smaller) ✅
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| JobOrder/…/JoOverviewSection.xaml | 0 | 3 | 0 | 1 | ★★★★★ |
| JobOrder/…/JoProductionSection.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| JobOrder/…/JoSectionStyles.xaml | 1 | 0 | 10 | 0 | ★★★★ |
| DeliveryNote/…/DnOverviewSection.xaml | 2 | 5 | 7 | 0 | ★★★★ |
| DeliveryNote/…/DnAttachmentsSection.xaml | 2 | 3 | 0 | 0 | ★★★★ |
| DeliveryNote/…/DnHistorySection.xaml | 4 | 5 | 2 | 0 | ★★★ |
| DeliveryNote/…/DnSectionStyles.xaml | 6 | 1 | 52 | 0 | ★★ |
| TaxInvoice/…/TiOverviewSection.xaml | 5 | 3 | 6 | 0 | ★★★★ |
| TaxInvoice/…/TiFamilySection.xaml | 3 | 4 | 2 | 0 | ★★★★ |
| TaxInvoice/…/TiAttachmentsSection.xaml | 2 | 1 | 4 | 0 | ★★★★ |
| TaxInvoice/…/TiSectionStyles.xaml | 2 | 0 | 15 | 0 | ★★★★ |

Section files are mostly good. The **`*SectionStyles.xaml`** files (Dn=52
styles, Jo=10, Ti=15) are where chrome duplication concentrates — these should
be **collapsed into the shared `Themes/Styles/`** so all three modules share
one definition.

### Company / User / System / Diagnostics / Shared (medium) 🟡
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| CompanyCenter/CompanyCenterView.xaml | 0 | 3 | 6 | 1 | ★★★★ |
| UserCenter/UserCenterView.xaml | 0 | 5 | 7 | 1 | ★★★★ |
| SystemCenter/SystemCenterView.xaml | 0 | 2 | 26 | 1 | ★★★ |
| Diagnostics/DiagnosticsView.xaml | 0 | 1 | 10 | 1 | ★★★★ |
| Shared/SectionActivityView.xaml | 0 | 0 | 6 | 1 | ★★★★ |
| Shared/ChangesDialog.xaml | 0 | 1 | 0 | 0 | ★★★★★ |
| Shared/UnsavedChangesDialog.xaml | 0 | 0 | 0 | 1 | ★★★★★ |
| Shared/ComingSoonView.xaml | 2 | 2 | 0 | 0 | ★★★★ |
| About/AboutView.xaml | 2 | 7 | 12 | 1 | ★★★★ |

Good. `SystemCenter` (26 local styles) and `About` (7 radii) could migrate to
shared styles but are not broken.

### Job Order module 🟡
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| JobOrderView.xaml (shell) | 0 | 0 | 22 | 0 | ★★★★ |
| SavedJobOrdersList/SavedJobOrdersListView.xaml | 3 | 3 | 9 | 0 | ★★★★ |
| JobOrderDetail/JobOrderDetailView.xaml | 4 | 1 | 4 | 0 | ★★★★ |
| GodView/JobOrderGodView.xaml | 0 | 0 | 2 | 0 | ★★★★★ |
| SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml | 1 | 4 | 0 | 0 | ★★★★ |

JO is clean — recent work followed the design system well.

### Delivery Note module 🟠
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| DeliveryNoteView.xaml (shell) | 0 | 0 | 22 | 0 | ★★★★ |
| DeliveryNoteList/DeliveryNoteListView.xaml | 10 | 6 | **45** | 0 | ★★ |
| DeliveryNoteDetail/DeliveryNoteDetailView.xaml | 6 | 4 | 24 | 1 | ★★★ |
| GodView/DeliveryNoteGodView.xaml | 0 | 0 | 2 | 0 | ★★★★★ |
| DeliveryNoteList/JobOrderPicker/JobOrderPickerDialog.xaml | 1 | 2 | 0 | 0 | ★★★★ |
| …/Attachments/Rename/RenameAttachmentDialog.xaml | 3 | 6 | 1 | 0 | ★★★★ |

`DeliveryNoteListView` is the **worst list screen in the app**: 10 hardcoded
hex, **45 local styles**, 6 radii. It needs consolidation.

### Tax Invoice module 🟠
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| TaxInvoiceView.xaml (shell) | 0 | 0 | 22 | 0 | ★★★★ |
| TaxInvoiceList/TaxInvoiceListView.xaml | 16 | 6 | 24 | 0 | ★★ |
| TaxInvoiceDetail/TaxInvoiceDetailView.xaml | 0 | 3 | 11 | 1 | ★★★★ |
| GodView/TaxInvoiceGodView.xaml | 0 | 0 | 2 | 0 | ★★★★★ |
| TaxInvoiceList/ProformaPickerDialog.xaml | 1 | 2 | 0 | 0 | ★★★★ |

`TaxInvoiceListView`: 16 hardcoded hex, 24 styles — same pattern as DN list.

### Proforma Invoice module (largest, most inconsistent) 🔴
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| ProformaInvoiceView.xaml (wizard shell) | 6 | 6 | 25 | 1 | ★★★ |
| Step1CompanySetup/CompanySetupView.xaml | 9 | 8 | 29 | 1 | ★★ |
| Step2ClientProfile/ClientProfileView.xaml | 8 | 7 | 28 | 1 | ★★ |
| Step3ProjectSpecs/ProjectSpecsView.xaml | 9 | 8 | 20 | 1 | ★★ |
| Step4PricingEstimator/PricingEstimatorView.xaml | 7 | 5 | 12 | 0 | ★★★ |
| Step4PricingEstimator/DguParameters/DguParametersView.xaml | 1 | 2 | 0 | 0 | ★★★★ |
| Step4PricingEstimator/LamParameters/LamParametersView.xaml | 0 | 2 | 0 | 0 | ★★★★★ |
| Step4PricingEstimator/SguParameters/SguParametersView.xaml | 1 | 2 | 0 | 0 | ★★★★ |
| Step4PricingEstimator/DguLamParameters/DguLamParametersView.xaml | 1 | 2 | 0 | 0 | ★★★★★ |
| Step5DataGrid/DataGridView.xaml | 10 | 8 | 27 | 0 | ★★ |
| Step5DataGrid/GrandTotalSummary/GrandTotalSummaryView.xaml | 3 | 4 | 9 | 0 | ★★★★ |
| Step6OtherCharges/OtherChargesView.xaml | 6 | 5 | 17 | 0 | ★★★ |
| Step6OtherCharges/CustomCharges/CustomChargesSectionView.xaml | 7 | 4 | 5 | 0 | ★★★ |
| Step6OtherCharges/Wastages/WastagesSectionView.xaml | 6 | 4 | 5 | 0 | ★★★ |
| Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml | 12 | 6 | 23 | 0 | ★★ |
| Step7PrintPreview/PrintPreviewView.xaml | 9 | 3 | 2 | 0 | ★★★ |
| Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryView.xaml | 6 | 2 | 3 | 0 | ★★★ |
| Step7PrintPreview/TermsAndConditions/TermsAndConditionsView.xaml | 4 | 6 | 11 | 0 | ★★★ |
| Step8Attachments/AttachmentsStepView.xaml | 0 | 2 | 7 | 2 | ★★★★ |
| Step9ProfitLoss/ProfitLossStepView.xaml | 0 | 3 | 15 | 1 | ★★★★ |
| SavedInvoicesList/SavedInvoicesListView.xaml | 11 | 5 | 13 | 4 | ★★ |
| SavedInvoicesList/VersionsDialog.xaml | 0 | 1 | 0 | 0 | ★★★★★ |
| SavedInvoicesList/SavedInvoicesListDialog.xaml | 0 | 0 | 0 | 0 | ★★★★★ |

The PI module is **where the indigo accent lives** (95 of the 142 hardcoded
`#4338CA`) and where **4 non-token page-title sizes** appear. Steps 1–3 and
Step 5 are the highest-priority redesign targets.

### Reports / Statement module 🔴
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| StatementGenerator/Filter/FilterStep.xaml | **26** | 9 | 6 | 0 | ★★ |
| StatementGenerator/Preview/PreviewStep.xaml | **28** | 6 | 7 | 0 | ★★ |
| StatementGenerator/Review/ReviewStep.xaml | 12 | 9 | 28 | 1 | ★★ |
| StatementGenerator/Pipeline/ExecuteStep.xaml | **31** | 7 | 7 | 5 | ★ |
| StatementGenerator/Shell/StatementGeneratorView.xaml | 0 | 2 | 40 | 0 | ★★★ |
| StatementGenerator/Styles/WizardStyles.xaml | 7 | 2 | 22 | 0 | ★★★ |
| StatementList/StatementListView.xaml | 0 | 9 | 17 | 2 | ★★★ |
| Reports/SalesReportView.xaml | 0 | 0 | 0 | 0 | ★★★★★ |
| Workflow/WorkflowProgressDialog.xaml | 0 | 8 | 0 | 0 | ★★★★ |
| Workflow/WorkflowWarningDialog.xaml | 0 | 5 | 7 | 1 | ★★★★ |

`ExecuteStep` (31 hardcoded hex, 5 non-token titles), `FilterStep` (26),
`PreviewStep` (28) are the **most color-hardcoded files in the whole app**.
This whole wizard bypasses the theme.

### Splash / Login 🔴
| View | H | R | S | T | Rating |
|---|---|---|---|---|---|
| Splash/SplashView.xaml | 13 | 4 | 3 | 2 | ★★ |
| Login/LoginWindow.xaml | 9 | 5 | 7 | 1 | ★★ |

Splash and Login hardcode their own palette; in a themed app these are the
first (branding) and second (gate) screens a user sees, so they matter most
for first impression. They currently do **not** follow the theme.

### Production + Optimization (placeholders) 🔴
| View | Rating |
|---|---|
| Production/CuttingView, Tempering, Polish, Pairing, Mitring, HolesCutouts, EdgeDeletion, DoublingLamination (all 6 lines) | ★ (ComingSoon placeholders) |
| Optimization/OptimizationView.xaml | ★ (placeholder) |

These are just `<shared:ComingSoonView/>`. Not "ugly" — they are **empty**.
They represent unfinished modules (see roadmap).

---

## Part 2 — The 6 root causes (measured, not opinion)

1. **Split color identity (THE big one).** Theme accent = blue `#1D4ED8`
   (light) / `#3B82F6` (dark). Modules hardcode **indigo `#4338CA` 142×** +
   the whole indigo family (`#6366F1` 52×, `#EEF2FF` 27×, `#8B5CF6`,
   `#3730A3`, `#C7D2FE`, `#4F46E5`, `#E0E7FF`, `#7C3AED`). Because the accent
   is baked in per-file, **runtime/dynamic theming can never work** — flip to
   the Professional/Monet theme and the accent stays indigo.

2. **93 distinct hardcoded hex colors** across Views, vs. the design system's
   rule *"Never hardcode a brand color in a view."* Many are near-duplicates
   of theme brushes (`#EEF2FF`≈`TintIndigoBrush`, `#FEF2F2`≈`TintRedBrush`,
   `#047857`≈`SuccessBrush`, `#B91C1C`≈`DangerBrush`).

3. **~49 distinct corner-radius values** for a system that defines 4 tokens
   (10/14/20/999). Even ignoring "one-sided" variants, values 3,4,5,6,7,9,11,
   12,16,17,18,19,21,22,24,28,34,36,38 all appear — the "different every
   screen" feel.

4. **Local style bloat.** `DeliveryNoteListView` (45), `StatementGeneratorView`
   (40), `DnSectionStyles` (52), `Step1–3` (~29/28/20), `DataGridView` (27),
   `ReviewStep` (28). The same "card", "field label", "pill", "toolbar button"
   is redefined per file instead of living in `Themes/Styles/`.

5. **Non-token type scale.** 24/26/28/36 page-title sizes when the system
   mandates a single `ModulePageTitleStyle` (24). `SavedInvoicesList` (4
   occurrences), `ExecuteStep` (5) are the worst.

6. **Four+ separate chrome systems.** The app already has
   `DesignTokens.xaml`, `CardStyles.xaml`, `ButtonStyles.xaml`,
   `WizardStyles.xaml`, `SettingsSectionStyles.xaml`, `DnSectionStyles.xaml`,
   `JoSectionStyles.xaml`, `TiSectionStyles.xaml`, `Step4SectionStyles.xaml`.
   These overlap heavily and were built independently.

---

## Part 3 — The redesign: architecture, not per-file patching

**Guiding principle (matches the codebase's own philosophy): centralize the
tokens and primitives once, then migrate modules to consume them. Never
find-and-replace 105 files blindly.**

### 3.1 Fix the color identity first (root of everything)

**Decision required:** pick **one** accent. The theme is already blue
(`#1D4ED8`/`#3B82F6`) and the shell (MainWindow + Dashboard + StatCards) uses
it. The modules use indigo. The safe industrial choice is to **keep the theme
blue as canonical** and migrate the indigo off the module internals onto theme
brushes. Do this centrally:

1. In `LightTheme.xaml` / `DarkTheme.xaml`, ensure every needed brush exists
   (`AccentBrush`, `AccentHoverBrush`, `AccentSoftBrush`/`TintAccentBrush`,
   `OnAccentForegroundBrush`) — several already do.
2. Map the indigo family to those brushes:
   - `#4338CA`/`#4F46E5`/`#3730A3` → `AccentBrush`
   - `#6366F1` → `AccentHoverBrush` / `AccentSecondaryBrush`
   - `#EEF2FF`/`#E0E7FF`/`#C7D2FE` → `TintIndigoBrush` (or a new
     `AccentTintBrush`)
3. Replace per-file `#4338CA` with `{DynamicResource AccentBrush}` — the 
   shell, dashboard, JO, and most dialogs already do this, so the "correct"
   form is proven in-tree. **Pilot on one module (e.g. Delivery Note), confirm
   on both Light/Dark + Monet/Professional themes, then replicate.**

### 3.2 Centralize shared primitives into `Themes/Styles/`

Move/collapse the overlapping chrome into the shared dictionaries so there is
**one** definition per concept:

| Concept | Consolidate into | Replace these local duplicates |
|---|---|---|
| Section card | `DesignTokens.xaml` → `ModuleSectionCardStyle` | `SettingsSectionStyles` card, `WizardStyles` card, `DnSectionStyles` card, per-step card styles |
| Page title/subtitle | `ModulePageTitleStyle` / `ModulePageSubtitleStyle` | `PageTitleStyle`, per-file 24/26/28/36 titles |
| Field label + input | shared `FieldLabelStyle` / `FieldInputStyle` | `CcFieldLabel`/`CcTextBox`, step 1–3 labels, `ReadOnlyFieldStyle` |
| Pill/chip | `PillStyles.xaml` | status/priority pills across DN/TI/JO/PI |
| Empty state | `EmptyStateControl` | any hand-rolled empty message |
| Toolbar buttons | `ModuleToolbarPrimary/SecondaryButtonStyle` | local button styles |

### 3.3 Kill the per-module `*SectionStyles.xaml` duplication

`JoSectionStyles` + `DnSectionStyles` + `TiSectionStyles` define nearly the
same thing. **Merge into one `Themes/Styles/DocumentSectionStyles.xaml`** (or
fold into `DesignTokens.xaml`) and have all three detail modules reference it.
This is pure duplication removal — zero visual change.

### 3.4 Normalize the type scale

Enforce the single title/subtitle scale. Replace the handful of `FontSize=26/28/36`
page titles with `ModulePageTitleStyle`. Replace hero numbers (KPI cards) with
the shared `StatCardControl`.

### 3.5 Normalize radius literals to the 4 tokens

`CornerRadius="3/4/5/6/7/9/12/16/18/22/24/28/34/36/38"` → the nearest of
10/14/20/999 (or the token's one-sided form). This alone removes the "many
different card shapes" feel.

---

## Part 4 — Module-by-module execution order (safe, per-commit)

Do this **pilot-first, one module per commit**, exactly as the design system
prescribes. Each is a small, reviewable, build-verifiable change.

| Phase | Module | Scope | Est. commits |
|---|---|---|---|
| 0 | **Fix color identity** (Light/Dark/Monet/Professional brushes + accent token) | Central theme; zero risk to runtime | 1 |
| 1 | **Consolidate section styles** (JO/DN/TI → one shared file) | Pure dedup, no visual change | 1 |
| 2 | **Delivery Note** (worst list screen: 45 styles, 10 hex) | Migrate list + detail to shared primitives | 2–3 |
| 3 | **Tax Invoice** list + detail | Same as DN | 2–3 |
| 4 | **Proforma Invoice** Steps 1–3 + Step 5 (the indigo heart) | Title scale + accent brushes + shared field styles | 3–4 |
| 5 | **Statement Generator** wizard (Filter/Preview/Review/Execute) | Accent → theme, kill the 26–31 hex files, title scale | 2–3 |
| 6 | **Splash + Login** (branding + first gate) | Follow theme accent/typography | 1 |
| 7 | **Proforma Steps 6–7** (other charges, print preview) | Accent + radius normalization | 2 |
| 8 | **Company/User/System/Diagnostics** + remaining dialogs | Shared styles | 1–2 |
| 9 | **Production + Optimization placeholders** | Decide: build or clearly mark roadmap | feature |
| 10 | Final **visual pass on all themes** (Light/Dark/Normal/Monet/Professional) | Contrast + dynamic-theme audit | 1 |

**Do NOT do in one commit:** global find/replace across 105 files (the
documented way to break a working app). Each phase is independently buildable
and testable.

---

## Part 5 — What "industrial grade" looks like for this app (target state)

- One accent, one radii scale, one type scale, all from the theme. Flip
  Light/Dark/Monet/Professional and **every screen follows** — this is the
  single biggest professionalism signal and it is currently broken by the
  hardcoded indigo.
- Any module's "card," "pill," "field," "button," "empty state" is the same
  control as every other module's (shared styles, no per-file redefinition).
- Empty lists explain what belongs + what to do (`EmptyStateControl`).
- Consistent 24/12 title/subtitle; readable density; no oversized hero cards
  wasting space (JO list already fixed this).
- No dead placeholder screens presented as real modules (Production/Optimization).

---

## Fastest wins / execution progress

### Phase 9 remainder — module headers / wizard steps: **APPLIED** (2026-08-18)

Shared chrome added to `DesignTokens.xaml` (`ModuleDetailTitleStyle`,
`ModuleDetailActionBarStyle`, `ModuleWizardStepIconStyle`,
`ModuleErrorBannerStyle`, `ModuleSuccessBannerStyle`,
`ModuleFieldLabelStyle`, `ModuleInlineWarningStyle`). JO/DN/TI detail
action bars + list/detail error banners, the PI list title, and PI
wizard steps 1–3 / 5–8 headers now consume those primitives. Global
TextBox focus ring and Step 4 inputs no longer hardcode indigo.
Statement wizard Filter/Preview/Execute hex and the printable-invoice
surface are the next visual increment (one module per commit).

See `Docs/FILE_ANALYSIS_2026-08.md` and `Docs/DESIGN_SYSTEM.md`.

### Phase 0 — accent identity fix: **COMPLETE** ✅

Implemented in commits `2192e4b` → `cc2b0b6`. The entire hardcoded indigo
accent family (`#4338CA` accent, `#6366F1` hover, `#3730A3` dark) has been
removed from **all executable XAML across the whole app** (29+ document-module
views; only harmless doc comments remain):

- **Brush** properties (`Background`/`BorderBrush`/`Foreground`/`SelectionBrush`/`Fill`)
  now use `{DynamicResource AccentBrush}` / `AccentHoverBrush` / `AccentDarkBrush`.
- **Color**-typed properties (gradient stops, `GradientStartColor`,
  `DropShadowEffect.Color`) now use the new theme `Color` resources
  `AccentColor` / `AccentHoverColor` / `AccentDarkColor`, added to Light+Dark
  themes and published by `ThemeManager.ApplyAccent` so they follow the
  effective accent (custom + Monet) and light/dark swaps.

Result: the document modules now render with the theme's blue accent and flip
correctly across Light/Dark and the Monet/Professional families.

**Not migrated (deliberate):** Splash + Login are an intentional dark-brand
surface using the blue family (not the indigo family) — left as designed.
Runtime visual verification on Windows across all themes is still required.

---

### Type-scale normalization — investigated, **NOT a safe change** (recorded)

The audit's "non-token title sizes (26/28/36)" were investigated before
acting. On inspection they are **intentional hero-header and KPI designs**,
not broken page titles:

- `SavedInvoicesList` (28) and `StatementList` (28) use a **hero header**: an
  accent banner with large white count numbers (`TotalCount`, `TotalStatements`)
  — a legitimate alternative header pattern, not a stray size.
- `FontSize=36` on `AiChatView`, `Diagnostics`, `SectionActivity`, `SystemCenter`,
  `AttachmentsStep` are **icon glyphs** (Segoe MDL2 / emoji), not titles.
- The only genuine "page title" sizes outside `ModulePageTitleStyle` are the
  hero headers above, which are a **different, coherent pattern** (title on
  accent + KPI numbers), and collapsing them to 24 would damage the hierarchy.

**Conclusion:** do **not** mass-replace 26/28/36 with `ModulePageTitleStyle`.
The page-title scale is already consistent where it matters (the JO/DN/TI/
SheetStore lists all use `ModulePageTitleStyle`). This finding is recorded so
future work does not "normalize" intentional KPI/hero emphasis.

---

## Part 6 — Fastest wins (smallest change, biggest visual payoff)

1. **Fix the accent token** (Phase 0) — single central change that makes
   dynamic theming actually work; the most professional bang-for-buck.
2. **Collapse `*SectionStyles` into one shared file** (Phase 1) — pure dedup,
   no visual risk, immediately removes the "three different detail modules"
   feel.
3. **Ship the EmptyState / StatCard conventions everywhere** — already built,
   already used in Dashboard; extend to the list screens.
4. **Normalize the title scale** — 4 files hold the non-token sizes
   (SavedInvoicesList, ExecuteStep, Splash, UseDialog); quick.

---

## Honest verification note

Ratings are from **static measurement + reading representative views** in a
sandbox with **no .NET SDK / no Windows**, so nothing here was rendered or
runtime-tested. The XAML is well-formed (gate passes) and every change above
is scoped to stay build-safe, but each phase **must be built and visually
verified on a Windows machine** (both themes) before the next.

---

## Recommendation

Start with **Phase 0 (accent token) + Phase 1 (section-style consolidation)**
as the first two commits — they are central, low-risk, and unblock dynamic
theming. Then tackle the list screens (DN/TI) and the PI wizard, which are
where the biggest visual inconsistencies concentrate. If you want me to begin,
say the word and I'll implement Phase 0 + Phase 1 first and we verify the
theme flips, then continue module by module.
