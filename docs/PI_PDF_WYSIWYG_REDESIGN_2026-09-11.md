# PI Step 7 / PDF Export — WYSIWYG Redesign (Senior Analysis)

Date: 2026-09-11
Scope: Proforma Invoice Step 7 print preview + PDF export + physical print

## Reported issues

1. **A layout "name" appeared as a raw string on the document/UI** —
   operators could not tell what it was; it read like leaked debug text.
2. **Print preview ≠ exported PDF** — the on-screen paper and the exported
   file were visibly different documents.
3. **All 5 PDF layouts looked unprofessional** — bad spacing, details that
   did not expand/align their rows, inconsistent chrome.

## Root-cause analysis (not symptoms)

### 1. Leaked layout-name strings

Two real surfaces printed technical layout strings:

- The engine footer appended the active theme's *display name* to every
  exported document (`PI-2026-001 · Classic Navy`). A customer-facing
  invoice must identify the **document**, not the skin that rendered it.
- The Step 7 switcher badge bound to `PrintLayoutViewModel.Kind`, showing
  the raw C# enum member (`FormalBand`, `SplitConsole`, `QuietForm`, …).

Both removed. The footer now carries only document id · printed timestamp ·
page X/Y. The badge now binds a curated style family label per theme
(`EXECUTIVE`, `MODERN`, `EDITORIAL`, `INDUSTRIAL`, `MINIMAL`).

### 2. Preview ≠ export — structural, not cosmetic

The preview is **one** XAML composition re-skinned by theme tokens
(`PrintTokenService` → `Print.*` resources). The export pipeline rendered
**five different page architectures** keyed off `PdfLayoutKind` inside
`PdfChrome` (five letterheads, five section-banner styles, five totals
bars, five signature blocks). Parity was impossible for 4 of 5 layouts by
construction — no amount of tuning could fix it.

A **third** renderer made it worse: the physical Print command rebuilt the
invoice in code-behind visuals with a hard-coded indigo palette, no logo
and no theming (`PrintableInvoiceViewModel.BuildPrintableDocument`).

### 3. The unprofessional details

- Spec-table headers used a rainbow `ExportColumnPalette` (blue/orange/
  green/purple column families) — reads as a debug view on a money
  document.
- Spec sub-totals were a full-width bar under the table instead of sitting
  under their QTY / TOTAL SQM / VALUE columns.
- Other-charges rendered a header table whose columns did not align with
  the body rows below it ("details not expanding the row").
- Per-kind margins/cell padding fought each other, so vertical rhythm was
  inconsistent between sections.

## The fix — one design, five styles

### Architecture

- New `PiPreviewParityRenderer` (+ `PiPreviewLetterhead`) — a faithful
  QuestPDF port of `PrintableInvoiceView.xaml`: same section order, same
  en-US labels, same column geometry (preview DIPs converted at 1 pt =
  3/4 DIP), same logo-placement rule
  (`PrintTokenService.IsTopPanelLogo` + company record layout), same cards,
  bands, zebra rows and signature blocks.
- Both pipelines read the **same** `PdfTheme`/`PdfMetrics` tokens, so the
  five styles re-skin screen and paper identically. Fixed measurements are
  converted, not re-authored.
- New engine hook: `IPdfLayout.LetterheadStyle` lets a module own its
  page-1 letterhead. PI supplies the preview-parity letterhead; every
  other module (JO/DN/TI/Statement/Sheet Store) keeps the shared themed
  chrome untouched.
- Printing (`ExecutePrint`) now renders the same QuestPDF layout through
  `PdfExportEngine.PrintAsync`. The legacy FixedDocument builder was
  deleted (~700 lines of a third design removed).

### What changed per section

| Section        | Before (PDF)                                    | After (PDF = preview)                                  |
|----------------|--------------------------------------------------|---------------------------------------------------------|
| Letterhead     | 5 divergent compositions                         | Masthead or identity + reference box, logo rules shared |
| Spec header    | rainbow 5-colour palette                         | uniform themed header, per-theme grid/zebra             |
| Spec sub-total | full-width bar                                   | cells under QTY / TOTAL SQM / VALUE (ColumnSpan)        |
| Grand total    | text-only band                                   | GRAND SPEC TOTAL band with specs · qty · sqm · value    |
| Other charges  | misaligned header + rows                         | 2/1.2/1/1 grid rows + OTHER CHARGES SUBTOTAL row        |
| Financials     | right column, drifting metrics                   | right-aligned waterfall + TOTAL EST VALUE band          |
| Signatures     | 5 variants                                       | the preview's two cards (prepared by / client approval) |
| Footer         | document id + **theme name**                     | document id · printed timestamp · page X/Y              |

### The five styles (redesigned)

| Style      | Id              | Character                                                        |
|------------|-----------------|------------------------------------------------------------------|
| EXECUTIVE  | ClassicNavy     | Deep navy + brass, zebra, balanced margins — formal commercial   |
| MODERN     | ModernTeal      | Teal/cyan, airy spacing, larger title scale                      |
| EDITORIAL  | EditorialInk    | Warm paper, black ink + copper rules, no zebra, wide margins     |
| INDUSTRIAL | IndustrialSteel | Graphite + safety amber, full table grid, dense cells            |
| MINIMAL    | MinimalIvory    | Ivory paper, charcoal hairlines, no zebra, maximum whitespace    |

Each differs in palette, page margin, rhythm, cell padding, zebra/grid and
type scale — never in page architecture, which is what guarantees WYSIWYG.

## Gates honoured

- `MoneyRoundingRatchetTests`: PI layout keeps exactly 7 unmode
  `Math.Round` call sites; the parity renderer adds none (amounts arrive
  pre-rounded); stale Part2 entry removed.
- `ExportCultureInvarianceTests`: new renderer added to the scan list; all
  numeric/date formatting passes `CultureInfo.InvariantCulture`.
- Amounts remain in AED (books' base currency); the on-screen display-
  currency conversion stays a screen-only convenience.

## Files

| File | Change |
|------|--------|
| `Services/ProformaInvoice/Export/PiPreviewParityRenderer.cs` | NEW — WYSIWYG renderer + letterhead + shared tokens/strings |
| `Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs` | Rewired to parity renderer; letterhead style; meta on LetterheadInfo |
| `Services/Export/Pdf/PdfTheme.cs` | `StyleLabel`; five themes redesigned |
| `Services/Export/Pdf/IPdfLayout.cs` | `IPdfLetterheadStyle` + default `LetterheadStyle => null` |
| `Services/Export/Pdf/PdfExportEngine.cs` | Honours module letterhead style; footer no longer prints the skin name |
| `Services/Export/Pdf/LetterheadInfo.cs` | `IssueDateText` for reference-box letterheads |
| `Services/Export/Pdf/Renderers/PdfChrome.Part1.cs` | `LoadLogoBytes` internal (one defensive logo loader for both pipelines) |
| `Services/Export/PrintPreview/PrintTokenService.cs` | `Print.Layout.StyleLabel` token; `Mix` internal |
| `ViewModels/Shared/PrintLayoutViewModel.cs` | `SelectedStyleLabel` |
| `Views/ProformaInvoice/Step7PrintPreview/PrintPreviewView.xaml` | Badge binds the curated label |
| `Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceView.xaml` | Remaining static paddings tokenized |
| `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs` | Print via shared engine; legacy builder deleted |
| `ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part2.cs` | Legacy render helpers deleted |
