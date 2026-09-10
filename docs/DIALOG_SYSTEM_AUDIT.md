# Dialog & Popup System — Senior Audit and Unified Architecture

**Date:** 2026-09-10 · **Scope:** every popup, dialogue, picker, prompt and tooltip in the app
**Goal:** one shared, professional dialog system so popups never have to be fixed one-by-one again.

---

## 1. Executive summary

The app renders **~28 dialog windows** and **263 message prompts**, and every single one
re-implements its own window chrome, sizing, header, footer and input styles. There is no
single source of truth, so the same class of bug (clipped text, too-small windows, misaligned
cards, unreadable tooltips) keeps re-appearing in module after module — Delivery Note
Delete/Void is simply where it was noticed most.

**Root cause is architectural, not cosmetic:** dialog chrome was copy-pasted per window
(~15 different shadow margins, ~5 header layouts, ~4 footer treatments), prompts go through
the **native OS `MessageBox`** (unthemed, tiny, non-wrapping), and there is **no implicit
ToolTip style**, so tooltips render with WPF's default whitish bubble in both themes.

**Fix delivered in this change set:** a shared dialog platform —

| Layer | What it is | Fixes |
|---|---|---|
| `ThemedDialogWindow` | One base class + one chrome template for **all** dialogs | window too small, misaligned cards, inconsistent headers/footers, missing close affordances, 768p overflow |
| `DialogStyles.xaml` (rewritten) | All dialog styles: chrome template, icon badge, title/subtitle, field labels, section cards, footer, **app-wide implicit ToolTip style** | "fix one by one" problem — styling now lives in exactly one file |
| `ThemedMessageBox` + rewired `DialogService` | Themed, wrapping, owner-aware replacement for `MessageBox.Show` used by `IDialogService` | the 263 prompt call sites (Delivery Note delete/void confirmations included) become professional **without touching any ViewModel** |
| Window migration | All modal dialogs re-based on `ThemedDialogWindow` | per-dialog drift eliminated |

---

## 2. Findings (evidence-based)

### F1 — Native `MessageBox` for user-facing prompts (highest impact)
`Services/Dialogs/DialogService.cs` implemented `Show/Info/Warning/Error/Confirm` as
`MessageBox.Show(...)`. **263 call sites** across 56 ViewModels/Services render through it —
including the entire **Delivery Note delete/void** flow
(`DeliveryNoteListViewModel.Part2.cs:209,245,281,302…`). Native message boxes:

- do not follow the Light/Dark theme or accent (always OS-styled, jarring in glass UI);
- cannot wrap or scroll long localized/exception text (15 shipped languages, incl. RTL) → the
  "broken text / cut-off text" reports;
- have a fixed small OS layout and tiny caption font — the "window too small" complaint;
- are visually disconnected from the app (owner highlight inconsistent).

### F2 — ~28 hand-rolled chrome implementations, all slightly different
Evidence (shadow margin / surface / header-height per file):

| Dialog | Size | Chrome notes |
|---|---|---|
| `VoidReasonDialog` | 540×420 **fixed, NoResize** | margin 24,20,24,28 · 44px icon · hand header/footer |
| `UnsavedChangesDialog` | 520×auto | **OS window** (no glass), custom button templates |
| `LanguageSuggestionDialog` | 480×auto | OS window, different card brush (`GlassUltraClearBrush`) |
| `RenameAttachmentDialog` | 560×400 fixed | margin shadow, own label style |
| `JobOrderAttachmentWarningDialog` | 560×380 fixed + **inner MaxWidth=400** | card narrower than window → misalignment |
| `ApprovalUploadChoiceDialog` | 560×440 fixed | 52px icon (vs 44 elsewhere) |
| `InvoiceApprovalDialog` | 560×520 fixed | re-declares `ApprovalFieldLabel` + `ApprovalTextBox` locally |
| `VersionsDialog` | 560×540 | OS window |
| `ChangesDialog` | 640×520 | OS window, two fixed `*` columns |
| `AuditReportDialog` | 880×820 | OS window — **820px tall ⇒ clipped on 768p laptops** |
| `IssueDraftsDialog` | 560×auto | own styles |
| `CrashReportDialog` | 560×380 | own `CrashReportDialogStyles.xaml` |
| `ShareComposerWindow` | 640×**780** NoResize | taller than 800px work areas ⇒ cut off on 1366×768 |
| `WorkflowWarningDialog` | 720×580 | margin 16 (vs 24/28) |
| `WorkflowProgressDialog` | 800×700 CanResize | OS title bar + `WindowStyle=None` mix |
| `JobOrderPickerDialog` / `JobOrderProformaPickerDialog` / `ProformaPickerDialog` | 780×620 / 820×660 / 720×580 fixed | grid pickers, hand headers |
| `BuyDialogView` / `UseDialogView` | 480 / 520 ×auto, **MaxHeight 720/760** | shadow margin **18**, **hard-coded `Color="Black"`** shadow, hand footer |
| `DuplicateResolutionView` / `HistoryView` / `ItemMovementView` | various | same pattern, third variant |
| `ForcePasswordChangeWindow` / `ResetPasswordWindow` | 420×620 / 440×640 fixed | 640px tall + margins ⇒ clipped on 768p |
| `SheetDetailWindow`, `InquiryEditorWindow`, `SavedInvoicesListDialog`, `LoginWindow` | big module windows | 14/8 local styles each |

Every file repeats: `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`,
a shadow-margin `Border`, an icon+title header (3 different icon sizes, 5 margin sets), a
footer (4 padding sets), `TextOptions.*` boilerplate. Fixing one bug means editing 28 files —
exactly the maintenance trap reported.

### F3 — Fixed `Width`/`Height` + `ResizeMode="NoResize"` ⇒ text cannot reflow
`VoidReasonDialog` (540×420), `InvoiceApprovalDialog` (560×520), `RenameAttachmentDialog`
(560×400), `ApprovalUploadChoiceDialog` (560×440), `ShareComposerWindow` (640×780),
`JobOrderProformaPickerDialog` (820×660) etc. size the *window*, not the *content*. Any
longer localized string (Urdu/Arabic strings run 20–40% longer than English) or Windows text
scaling >100% overflows and is clipped — "text is not visible / window too small".

### F4 — No implicit `ToolTip` style
Only a *keyed* `CrystalToolTipStyle` exists (opt-in). Every `ToolTip="…"` in the app
(60+ files) renders with the **default WPF tooltip**: white bubble, no wrapping, wrong theme
in Dark mode → "tooltips not properly visible". Long tooltips even run off-screen because
`ToolTip.MaxWidth` is unlimited by default.

### F5 — Misaligned cards inside dialogs
Per-dialog margins (28 / 24 / 22 / 20 / 18px), three icon badge sizes (40/42/44), and ad-hoc
"inset card" borders (`DialogInsetBrush` vs `WindowBackgroundBrush` vs `ElevatedSurfaceBrush`,
paddings 14,10 / 12,9 / 10,6) produce visibly inconsistent inner cards. The two-column
before/after layout in `ChangesDialog` squeezes both cards to 50% width and wraps badly on
narrow windows.

### F6 — Off-center windows (transparent shadow trick)
`CenterOwner` centers the *transparent outer rect* including the shadow margin
(24,20,24,28 vs 18 vs 16). Asymmetric margins shift the visible card up-left by
(4,8)px — subtle "misalignment" on every borderless dialog.

### F7 — Inconsistent keyboard semantics
Some dialogs wire `KeyBinding Escape→CancelCommand` (BuyDialogView), some rely on `IsCancel`,
several have neither; several have no close affordance at all; default (Enter) buttons vary.

---

## 3. Target architecture

```
                       ┌──────────────────────────────────────────────┐
                       │ DialogStyles.xaml  (single source of truth)  │
                       │  • implicit chrome template for              │
                       │    ThemedDialogWindow (card/header/body/     │
                       │    footer/close/shadow/drag)                 │
                       │  • Dialog* shared styles + tokens            │
                       │  • implicit app-wide ToolTip style           │
                       └───────────────┬──────────────────────────────┘
                                       │
        ┌──────────────────────────────┼───────────────────────────────┐
        │                              │                               │
┌───────▼──────────────┐   ┌───────────▼─────────────┐   ┌─────────────▼──────────┐
│ ThemedDialogWindow   │   │ ThemedMessageBoxWindow  │   │ 28 dialog windows      │
│ base class           │◄──│ (MessageBox replacement)│   │ (migrated: derive from │
│ ESC/drag/clamp/owner │   └───────────┬─────────────┘   │ ThemedDialogWindow)    │
└───────┬──────────────┘               │                 └────────────────────────┘
        │                              │
        │                  ┌───────────▼─────────────┐
        └──────────────────│ DialogService.Show /    │  ──► 263 prompt call sites
                           │ Info/Warning/Error/     │      (incl. DN delete/void)
                           │ Confirm → Themed box,   │
                           │ native fallback         │
                           └─────────────────────────┘
```

### Behavioural contracts of `ThemedDialogWindow`
- **Chrome:** one rounded glass card (`DialogSurfaceBrush` + outline + `ShadowElevated`),
  one header (icon badge 42px, title wraps, subtitle, close button with `Action.Close`
  tooltip), one body (content, smooth scroll), one footer (secondary strip, right-aligned).
- **Sizing:** `SizeToContent=Height` by default — windows grow with wrapped text, never clip;
  hard-clamped to the monitor **work area** (90% H / 94% W) in `OnSourceInitialized` and
  `OnContentRendered`, so nothing can ever open taller than the screen; body scrolls when the
  clamp engages. `WindowChrome` resize border is attached so `CanResize` dialogs remain
  resizable without an OS frame.
- **Centering:** `CenterOwner` with symmetric shadow margin → visually centered on the owner.
- **Keyboard:** `Esc` closes (combo dropdown-safe), dialog buttons keep `IsDefault`/`IsCancel`.
- **Theming:** everything is `DynamicResource` — Light/Dark + accent react live.

### `ThemedMessageBox` contract
Mirrors `MessageBox.Show(message, caption, button, icon, defaultResult)`; maps icons to
theme tint glyphs, Yes on destructive prompts to the danger pill, wraps + scrolls long text,
plays the matching system sound, owner-aware, STA-marshalled, with automatic native fallback
if window creation ever fails (tests, headless hosts).

---

## 4. Migration map (this change set)

| Window | Migration |
|---|---|
| Shared: VoidReason, UnsavedChanges, LanguageSuggestion, Changes, AuditReport | → `ThemedDialogWindow` chrome |
| Proforma: InvoiceApproval, ApprovalUploadChoice, Versions | → chrome |
| DeliveryNote: RenameAttachment, JobOrderPicker | → chrome |
| JobOrder: JobOrderAttachmentWarning, JobOrderProformaPicker | → chrome |
| TaxInvoice: IssueDrafts, ProformaPicker | → chrome |
| Reports: WorkflowWarning, WorkflowProgress | → chrome |
| Sharing: ShareComposer | → chrome (780px height → auto-size, fixes 768p clipping) |
| SheetStore: Buy, Use, DuplicateResolution, History, ItemMovement | → chrome (unified shadow/footer, opaque inset cards) |
| Crash: CrashReportDialog | → chrome (crash-path safe: OS-frame fallback if App resources unavailable) |
| Login: ForcePasswordChange, ResetPassword | **intentionally unchanged** — they belong to the pre-shell auth visual language (`AuthSurfaceStyles`/navy glass), like `LoginWindow` |
| Raw `MessageBox.Show` (views/services/app shell, ~40 sites) | → `Prompts.Show` → themed box (native fallback until App start) |
| `DialogService` | → `ThemedMessageBox` (native fallback) |
| ToolTips | implicit app-wide style centralized in `DialogStyles.xaml` (previously shadowed from WizardStyles) |
| Big module windows (SheetDetail, InquiryEditor, SavedInvoicesList, Login) | intentionally unchanged (they are pages, not popups) — they already consume shared token styles |

**Rule going forward:** new popups **must** derive `ThemedDialogWindow` and use
`Dialog*` styles from `DialogStyles.xaml`. Chrome/layout fixes now happen in exactly one file.

---

## 5. Verification checklist used for every migrated window
1. All `x:Name` elements preserved (code-behind bindings intact).
2. All `Binding`s / `DynamicResource` keys preserved.
3. XML well-formedness validated.
4. Window sizing: content wraps, clamps to work area, scrolls when clamped.
5. Esc / Enter / close-button semantics preserved per dialog.
