# Print Layout Unification — Senior Analysis & Upgrade

**Date:** 2026-09-08
**Scope:** Step 7 print preview ↔ PDF export parity, all five PDF layouts, all modules
**Status:** Implemented. **Not compiled** — see *Verification* at the end.

---

## 1. The report, and what was actually wrong

The report was: *Step 7 preview looks professional, the exported PDF does not, the two
differ, colours and spacing in the PDF are wrong, the preview has no logo, and the five
PDF layouts are not visible in the preview.*

Every part of that is correct. But the cause is not "the PDF spacing was hand-tuned
badly". It is structural, and worse than reported:

### Finding 1 — There were two unrelated design systems (root cause)

| | Step 7 preview | Exported PDF |
|---|---|---|
| Renderer | Hand-built WPF XAML | QuestPDF via `PdfChrome` |
| Colour source | 40 hardcoded slate literals + **UI theme brushes** | `PdfTheme` tokens |
| Spacing source | Hand-typed `Padding="40,32"`, `6,5`, `Margin="0,0,0,16"` | Hand-typed literals |
| Reads the other? | **No** | **No** |

Verified: `grep -rn "PdfTheme\|DocumentStyle\|PdfChrome"` across
`Views/ProformaInvoice/Step7PrintPreview/` returned **one** hit — the export call itself.
The preview never read a single print token. Switching *Settings → PDF Layout* changed
the export and left the preview untouched, so the two could never agree in **any** of the
five layouts.

### Finding 2 — The preview rendered the *UI* theme, not a print theme (most serious)

The preview used `AccentBrush` (14×), `PrimaryForegroundBrush` (11×),
`SecondaryForegroundBrush` (11×). Those are defined separately in `DarkTheme.xaml` and
`LightTheme.xaml`:

```
DarkTheme.xaml:22   PrimaryForegroundColor  #E5E7EB   (near-white)
LightTheme.xaml:43  PrimaryForegroundColor  #111827
DarkTheme.xaml:36   AccentColor             #38BDF8   (sky blue)
LightTheme.xaml:58  AccentColor             #0F6FA8
```

**In dark mode the invoice preview drew near-white text on white paper.** And in both
modes its accent matched *neither* of the five PDF layouts. A money document was
inheriting the operator's UI theme — the exact thing `DocumentStyle`'s own doc comment
forbids ("Printed documents stay light-mode — these are not UI theme brushes").

### Finding 3 — No logo in the preview

`grep -rn -i "logo"` across the Step 7 preview returned **zero** matches, while the PDF
has a full logo pipeline (`LoadLogoBytes`, `LogoBox`, `TopPanelMasthead`). Operators were
approving a document that did not look like the one they sent.

### Finding 4 — The PDF had no vertical rhythm

`PdfTheme` exposed four numbers (`MarginMm`, `ContentSpacing`, `CellPadH`, `CellPadV`).
Everything else was **239 hand-typed literals** in `PdfChrome` — `PaddingHorizontal(12)`,
`FontSize(6.5f)`, `PaddingTop(22)`, `ConstantItem(210)`. Not a scale, a pile of guesses.
That is precisely the "too big or too small" symptom.

### Finding 5 — Ragged label/value alignment

`PdfChrome.LabelValue` used `AutoItem()` for the label, so every value started at a
different x. Every card in every document read ragged.

### Finding 6 — Fixed-width blocks crushing layout

`ConstantItem(210)` in the Formal letterhead = **41%** of A4 portrait content width
(507 pt) given to the document-reference block, squeezing the company identity beside it.
Same defect at `ConstantItem(205)` (Workshop) and `ConstantItem(170)` (Editorial).

### Finding 7 — Subtotals painted green

`SubtotalBar` rendered the amount in `T.Success`. Green on a money document reads as
*profit*. A subtotal is neither profit nor loss.

### Finding 8 — Section numbering is process-global mutable state

`PdfChrome._sectionIndex` was a plain `static int`, mutated while `GeneratePdf` runs on a
`Task.Run` worker. Two concurrent exports interleave their `SEC 01 / SEC 02` counters.

### Finding 9 — Tax Invoice never reset that counter

`TaxInvoicePdfExporter.PdfDocumentWrapper.Compose` built its own page and never called
`PdfChrome.Reset()`. It only got reset by accident, via `Letterhead()` — so a Tax Invoice
with no letterhead inherited the previous export's numbering.

### Finding 10 — Specifications bypassed the system entirely

`SpecificationsPdfLayout` used no chrome: no letterhead, no section headers, no zebra,
`FontSize(8)` / `PaddingVertical(3)` hardcoded. Switching layout recoloured it but never
re-architected it.

### Finding 11 — Internal jargon printed on customer documents

`"COMMERCIAL / PRODUCTION ERP DOCUMENT"`, `"BOARD / CLIENT PDF PACK"`,
`"CONTROLLED PDF"`, `"ERP DOCUMENT"`. The client cannot act on any of these.
(`"FACTORY RELEASE · QA TRACEABLE · CONTROLLED COPY"` was **kept** — on a shop-floor
docket, "controlled copy" is a real ISO-9001 document-control statement.)

### Finding 12 — An undated date

The QuietForm letterhead printed `DateTime.Now` with no label, directly above the
document's own issue date. Two dates, no explanation.

---

## 2. What was built

### 2.1 One derived rhythm scale — `Services/Export/Pdf/PdfMetrics.cs` (new)

Replaces the 239 literals with a scale derived from two axes, and only two:

- **KIND → SHAPE.** Rail weights, border weights, hairline weights. Identity. A workshop
  docket draws a 4 pt amber rail; Editorial draws a 1.2 pt copper rule. These deliberately
  do **not** scale with density.
- **`ContentSpacing` → RHYTHM.** Every gap, pad and font size is a ratio of the theme's
  own `ContentSpacing` (7.5 Workshop … 15 QuietForm).

Nothing is authored per theme, so the five layouts stay internally consistent and a sixth
layout would get a correct rhythm for free. Measured result:

```
layout          kind          Sec   Blk   Row   Stk   Gut  Card  SigH   Tbl   Hdr  GdVal  LblCol  MrgDIP
ClassicNavy     FormalBand    9.5   5.9   2.9   1.7  10.5   7.8  29.4   8.0   7.6   12.4    73.3    58.6
ModernTeal      SplitConsole 11.0   6.8   3.3   2.0  12.1   9.0  34.1   8.0   7.6   12.4    71.4    54.8
EditorialInk    Editorial    13.0   8.1   3.9   2.3  14.3  10.7  40.3   8.0   7.6   12.4    70.0    71.8
IndustrialSteel Workshop      7.5   4.7   2.2   1.3   8.2   6.1  23.2   7.4   7.0   11.5    58.9    43.5
MinimalIvory    QuietForm    15.0   9.3   4.5   2.7  16.5  12.3  44.0   8.0   7.6   12.4    85.8    79.4
```

Grand total went from a flat 13 pt against an 8 pt table (1.63×, unrelated to the theme)
to a constant **1.55×** of that layout's own table size.

### 2.2 `PdfChrome` refactored onto the scale — all 13 consumers at once

239 literals → 3 (one is an intentional 0.01 pt zero-height spacer). Because every module
renders through `PdfChrome`, all of PI / JO / DN / TI / Dossier / Statement / Order Report
/ Sheet Store / Cut List inherit the fix with no per-module work. Also fixed here:
`LabelValue` now uses a shared `LabelColumn` width (Finding 5); `ConstantItem` blocks
became proportional (Finding 6); subtotals are neutral (Finding 7); jargon replaced by the
company's real contact line (Finding 11); the QuietForm date is labelled `PRINTED`
(Finding 12); the SplitConsole TRN chip got `AlignLeft()` — it had been stretching a navy
bar the full card width behind a handful of characters.

### 2.3 The preview now reads the same tokens — `PrintTokenService` (new)

Mirrors the active layout into WPF resources under `Print.*` keys, and re-publishes on
`DocumentStyle.Changed`, so the paper re-skins live.

**The unit conversion is what makes the match exact rather than approximate:**

```
QuestPDF measures in POINTS          (1 pt  = 1/72 in)
WPF measures in DIPs                 (1 DIP = 1/96 in)
therefore  DIP = pt x 96/72 = pt x 4/3
```

The preview paper is 794 DIP wide; A4 is 595.276 pt; `595.276 × 4/3 = 793.7 ≈ 794`. So the
preview already sits at exactly 4/3 print scale, and applying `PtToDip` to every token puts
the on-screen document at true 100% A4. A 15.5 mm margin is 44 pt on paper and 58.6 DIP on
screen — one number, three units.

The preview's hardcoded `Padding="40,32"` was 10.6 mm / 8.5 mm for **every** layout; it is
now the active layout's real margin (43.5 – 79.4 DIP).

### 2.4 The preview stopped using the UI theme

All 38 UI-theme brush references inside the paper replaced with `Print.*`. Final counts in
`PrintableInvoiceView.xaml`: **0** hex literals, **0** UI-theme brushes, **0** UI
type-scale references. It is now a light-mode print document in every app theme.

### 2.5 Logo added to the preview, sharing the exporter's rules

`CompanySetupViewModel` gained `LogoImage` (defensive `OnLoad` + `Freeze` decode — a moved
or corrupt file blanks the chip, never breaks a money document), `LogoIsTopPanel`,
`LogoIsInline`, `LogoWidthDip`, `LogoHeightDip`. The clamp bounds and the wide-mark rule
are **shared constants** (`PrintTokenService.LogoMinWidthMm`, `IsTopPanelLogo`) consumed by
both `PdfChrome` and the VM, so preview and PDF cannot disagree about size or slot. The
TopPanel masthead is a faithful identity band (logo + company + TRN + document reference),
and the inline company block collapses when the masthead carries it — no duplicate company.

### 2.6 Live five-layout switcher on the preview

`PrintLayoutViewModel` (new) + a picker above the paper. It **persists** to
`Appearance.PdfLayoutId`, because `PdfExportEngine` calls `PdfThemeCatalog.ApplySaved()`
before rendering — a switcher that only changed the in-memory theme would be silently
reverted at export time, recreating the exact divergence this change exists to remove.

### 2.7 Module parity

`SpecificationsPdfLayout` rebuilt onto `PdfChrome` (section header, KPI strip, zebra,
density response) with its own catalogue content — not a copy of PI's fields. Font-size
literals eliminated in Tax Invoice (10), Statement (33), Order Report (39), Sheet Store (7).
Proforma Invoice layout taken to **0** literals.

### 2.8 Correctness

`_sectionIndex` is now `[ThreadStatic]` and reset **inside** the composition delegate, so
it resets on the generating thread. `TaxInvoicePdfExporter` now resets explicitly.
`DocumentStyle` gained a `Changed` event (guarded, so a broken preview subscriber can
never break printing).

---

## 3. Verification — what ran and what could not

**Could not compile.** This sandbox has no .NET SDK and no package source:

```
dotnet, mono, msbuild, csc      -> not installed
https://dot.net                 -> 000 (blocked)
https://api.nuget.org           -> 000 (blocked)
raw.githubusercontent.com       -> exit 35 (SSL connect error)
~/.nuget/packages               -> does not exist
```

`net8.0-windows` + `UseWPF` also needs a Windows targeting pack. **No build, no test run,
no runtime check was possible.** Everything below is static.

| Check | Result |
|---|---|
| XAML well-formedness (all `.xaml` in repo) | **0 failures** |
| Every `Print.*` key referenced resolves in `PrintTokenService` | **0 missing** (43 used / 79 defined) |
| Every `M.*` metric resolves on `PdfMetrics` | **0 undefined** (52 used / 56 declared) |
| Every `PdfChrome.*` call matches a public member | **0 unresolved** (16 called / 18 public) |
| Brace / paren / bracket balance, proper lexer, all 20 touched `.cs` | **0 unbalanced** |
| Remaining hardcoded literals in `PdfChrome` | 239 → **3** |
| Remaining `FontSize(n)` literals in module layouts | **0** |
| Preview hex literals / UI brushes / UI type scale | **0 / 0 / 0** |
| `DocumentStyle.Current` assigned outside `DocumentStyle` | **none** |

Two files were briefly flagged unbalanced by a naive counter; both were artifacts —
`StatementPdfDocument` matches its `HEAD` baseline exactly (interpolated strings), and
`CompanySetupViewModel` contains `'"'`, a char literal holding a quote. A lexer that
handles char literals reports both balanced.

Two bugs were caught by these checks mid-change and fixed: a replacement pass that
stripped attribute quotes (27 sites) and one that dropped attribute *names* (48 sites).
Both would have been silent runtime failures.

**What is genuinely unverified:** that the C# compiles, and how the output actually looks.
Those need a Windows build. The scale arithmetic above was replayed independently to
confirm the numbers are sane (monotonic density, clamps engaging only on QuietForm's
signature height) — that validates the *math*, not the code path.

---

## 4. What I'd do next (not done)

1. **Build on Windows and eyeball all five layouts × PI/JO/DN/TI.** Nothing replaces it.
2. **Statement / Order Report / Sheet Store spacing.** Their *font sizes* are on the scale
   now; their bespoke paddings (175 literals) are not. They need the same read-through the
   other modules got.
3. **Real WYSIWYG.** The honest end state is rendering the QuestPDF output into the
   preview (rasterise to `WriteableBitmap`). That removes the two-renderer problem instead
   of disciplining it. Needs a PDF rasteriser — a real dependency decision, and the reason
   it isn't in this change.
4. **Contact fields on `LetterheadInfo`** are wired but only PI-adjacent modules populate
   `CompanyEmail` / `CompanyPhone`. The rest will render no contact line until they do.
5. **`PrintLayoutViewModel.SelectLayoutCommand` is currently unused** — the picker binds
   `SelectedId` directly. It exists as the hook for other modules' previews; delete it if
   that never happens.
6. **Zebra in the preview** relies on `ItemsControl.AlternationIndex` reaching the row
   `Border` through a `ContentPresenter`. Worth confirming visually — it is the one binding
   here with a non-obvious ancestor path.
