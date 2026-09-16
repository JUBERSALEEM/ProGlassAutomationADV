// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Audit/RuntimeSemanticsAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Runtime-semantics regression locks (wave 6)
//
// WHY THIS FILE EXISTS
//   Four defects survived the 2026-09-17 wave even though every existing
//   lock was green, because those locks checked the SHAPE of the source
//   ("does the attribute contain RelativeSource?", "does the width fit?")
//   instead of the SEMANTICS of the engine that consumes it. The owner's
//   verdict: "Do a proper senior cross check instead of blindly verifying
//   broken things."
//
//   Each lock below models the runtime rule that was actually violated:
//
//   • QuestPDF measures an AutoItem child with INFINITE width, so a
//     RelativeItem anywhere inside that subtree has no finite space to
//     divide and the whole document fails with "Conflicting size
//     constraints … wrap reason: forward from child". Every export died.
//   • WPF clamps a Grid child whose Grid.Row/Span exceeds the declared
//     tracks into the LAST track — silently painting it on top of whatever
//     lives there (Companies hub: "Outstanding" over "Invoiced").
//   • A DataGridColumn is in neither the visual nor the logical tree, so a
//     binding on a column property has no DataContext and no ancestor to
//     climb (locked in SettingsRedesignAuditTests).
//   • A card whose fill equals its stage and whose border is an 8% hairline
//     has no visible boundary: the iOS 26 light ramp painted every level
//     white and the page read as one empty sheet.
//   • Dark small text (Typography.Label) on a dark band (Print.TableHeader)
//     is unreadable and drifts from the exporter, which paints white bold.
//   • Option tiles inside a settings card touched each other because a
//     UniformGrid has no gap property and nobody gave the children one.
//
//   All checks are source scans — no WPF Application, no QuestPDF render.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Audit
{
    public sealed class RuntimeSemanticsAuditTests
    {
        // ────────────────────────────────────────────────────────────────
        //  1. QUESTPDF MEASUREMENT TREE
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void QuestPdf_AutoItem_Never_Contains_A_RelativeItem()
        {
            // An AutoItem hands its child INFINITE width. A RelativeItem needs
            // a finite parent to take a share of, so the Row forwards an
            // unsatisfiable constraint upward and QuestPDF throws
            // "Conflicting size constraints. … wrap reason: forward from child"
            // — the exact failure the owner hit on every proforma export
            // (PiPreviewParityRenderer.FinancialSummary used
            // outer.AutoItem().MinWidth(…) around weighted SummaryRows).
            //
            // Width arithmetic cannot catch this: the numbers looked fine.
            // Only the Auto/Relative PARENT-CHILD relationship matters, so the
            // lock walks each AutoItem's own chained call subtree.
            foreach (var file in CSharpExportFiles())
            {
                var code = StripCommentsAndLiterals(File.ReadAllText(file));
                for (int at = code.IndexOf(".AutoItem(", StringComparison.Ordinal);
                     at >= 0;
                     at = code.IndexOf(".AutoItem(", at + 1, StringComparison.Ordinal))
                {
                    string chain = ChainedCall(code, at);
                    Assert.False(chain.Contains("RelativeItem", StringComparison.Ordinal),
                        Path.GetFileName(file) + ": an AutoItem subtree contains a " +
                        "RelativeItem. AutoItem measures its child with infinite " +
                        "width, so the RelativeItem has no finite space to divide " +
                        "and QuestPDF fails the document with \"conflicting size " +
                        "constraints / forward from child\". Use weighted " +
                        "RelativeItems on both sides (the GrandSpecBand pattern), " +
                        "or ConstantItem for a truly fixed cell. Offender: " +
                        Truncate(chain));
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  2. WPF GRID TRACKS
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Grid_Children_Never_Exceed_Their_Declared_Tracks()
        {
            // WPF does not error when Grid.Row is out of range — it clamps the
            // child into the LAST track, so two elements paint on top of each
            // other. Companies hub: DOCUMENT ACTIVITY declared five rows and
            // placed children in rows 0…5, putting "Outstanding" on top of
            // "Invoiced" right after the Tax Invoices row.
            foreach (var file in XamlFiles("Views"))
            {
                var doc = TryLoad(file);
                if (doc == null) continue;
                foreach (XmlElement grid in doc.GetElementsByTagName("Grid"))
                {
                    int rows = TrackCount(grid, "Grid.RowDefinitions", "RowDefinition");
                    int cols = TrackCount(grid, "Grid.ColumnDefinitions", "ColumnDefinition");
                    foreach (XmlElement child in Children(grid))
                    {
                        int r = Index(child, "Grid.Row"), rs = Math.Max(1, Index(child, "Grid.RowSpan"));
                        int c = Index(child, "Grid.Column"), cs = Math.Max(1, Index(child, "Grid.ColumnSpan"));
                        Assert.True(rows == 0 || r < 0 || r + rs <= rows,
                            Offender(file, child) + " sits in row " + r + " (span " + rs +
                            ") of a grid that declares " + rows + " row track(s). WPF " +
                            "clamps it into the last track and it paints over that " +
                            "track's content. Declare the track or fix the index.");
                        Assert.True(cols == 0 || c < 0 || c + cs <= cols,
                            Offender(file, child) + " sits in column " + c + " (span " +
                            cs + ") of a grid that declares " + cols + " column " +
                            "track(s) — same silent clamp, same overlap.");
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  3. SETTINGS OPTION-TILE GUTTER
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Settings_Option_Tiles_Never_Touch_Each_Other()
        {
            // Owner: "cards under cards of options too close — give the same
            // spacing inside cards' options as the main cards." A UniformGrid
            // has no gap property and SettingsInnerTileStyle carries no Margin
            // (deliberately: 199 sites, many of them a single full-width tile,
            // where a global margin would misalign the tile against the card
            // padding). So the gutter has to be given per container:
            //   • stacked tiles  → SettingsStackGap14 (0,14,0,0) on each tile
            //     after the first;
            //   • tile grids     → SettingsTileGridMargin (7,7) on every child
            //     plus SettingsTileGridBleed (-7,-7,-7,7) on the grid, which
            //     reproduces the 14 px card rhythm on both axes while keeping
            //     the outer edges flush with the card content box.
            // Eight containers violated this; the lock keeps them fixed.
            foreach (var file in XamlFiles(Path.Combine("Views", "Settings")))
            {
                var doc = TryLoad(file);
                if (doc == null) continue;
                foreach (var container in new[] { "StackPanel", "UniformGrid", "Grid", "WrapPanel", "DockPanel" })
                {
                    foreach (XmlElement element in doc.GetElementsByTagName(container))
                    {
                        if (container == "StackPanel" &&
                            element.GetAttribute("Orientation") == "Horizontal") continue;
                        int touching = Children(element).Count(k =>
                            k.GetAttribute("Style").Contains("SettingsInnerTileStyle", StringComparison.Ordinal) &&
                            k.GetAttribute("Margin").Length == 0);
                        Assert.True(touching < 2,
                            Path.GetFileName(file) + ": a " + container + " holds " + touching +
                            " option tiles with no Margin, so they touch. Give stacked " +
                            "tiles SettingsStackGap14, or a tile grid " +
                            "SettingsTileGridMargin on every child plus " +
                            "SettingsTileGridBleed on the grid.");
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  4. PRINT TABLE-HEADER PARITY
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Print_Table_Header_Labels_Use_The_White_Bold_Header_Type()
        {
            // Owner: "header font text is small black, should be white — the
            // same type as the Specification section band". Print.TableHeader
            // is a DARK band in every PdfTheme (#0B2E59, #0F766E, #1B1B18,
            // #1C2833, #222933) and Typography.Label is FontSize.Label with
            // TextSecondaryBrush — dark ink. The exporter's SpecHeaderCell has
            // always painted HeaderFont + Bold + AccentOn, so the preview also
            // broke preview ≡ PDF parity. One shared style now carries that
            // cell into XAML; nothing else may label a header band.
            foreach (var file in XamlFiles("Views"))
            {
                var doc = TryLoad(file);
                if (doc == null) continue;
                foreach (XmlElement border in doc.GetElementsByTagName("Border"))
                {
                    if (!border.GetAttribute("Background").Contains("Print.TableHeader", StringComparison.Ordinal))
                        continue;
                    foreach (var label in Descendants(border)
                                 .Where(t => t.LocalName == "TextBlock" && t.GetAttribute("Grid.Column").Length > 0))
                    {
                        Assert.Contains("PrintTableHeaderTextStyle", label.GetAttribute("Style"));
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  5. THEME LEVEL SEPARATION
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Ios26_Light_Stage_And_Card_Are_Different_Levels()
        {
            // Owner: "visual theme page is now empty … defined edge cards
            // broken, surface level broken — these were working properly
            // before". The iOS 26 light table painted Window #FFFFFF→#FAFAFC,
            // Module #FFFFFF and Surface.Primary a 96%→91% translucent white
            // over that white module, with an 8% black hairline as the card
            // edge: a card whose fill equals its stage and whose border is
            // near-invisible has no boundary, so the page read as one empty
            // sheet. The painter had been dead until the 2026-09-17 dispatch
            // fix wired Ios26 up, which is why the levels "worked before".
            //
            // Invariants, straight from Apple's own light ramp
            // (systemGroupedBackground stage, opaque white cards):
            //   • the stage colour and the card colour must differ;
            //   • the card border must carry at least 10% alpha.
            var source = File.ReadAllText(Path.Combine(
                Root(), "Services", "Theme", "VisualThemeEngine.Ios26.cs"));
            string table = LightTable(source);

            string stage = RgbOf(table, "WindowBackgroundBrush") ?? RgbOf(table, "ModuleBackgroundBrush");
            string card = RgbOf(table, "Surface.Primary.BackgroundBrush");
            Assert.True(stage != null && card != null,
                "iOS 26 light table must publish a stage brush and Surface.Primary.BackgroundBrush.");
            Assert.NotEqual(stage, card);

            int alpha = AlphaOf(table!, "Surface.Primary.BorderBrush");
            Assert.True(alpha >= 0x1A,
                "iOS 26 light Surface.Primary.BorderBrush is " + alpha.ToString("X2", CultureInfo.InvariantCulture) +
                " alpha — a card edge below 10% black is invisible on a light " +
                "stage, which is how the theme came to look empty. Apple's own " +
                "light separator is ~12-14%.");
        }

        // ────────────────────────────────────────────────────────────────
        //  6. REPORT-HUB HEADER GRAMMAR
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void Report_Hub_Table_Headers_Use_The_Proforma_Grammar()
        {
            // Owner: "vat submission column still not matching proforma
            // invoice reports hub column header type". The hub grammar is
            // ReportTableHeaderStyle (accent bar) + ReportHeaderTextStyle
            // (white bold) on every column label — PiReportView is the
            // reference.
            //
            // A header is identified the way the eye identifies it: its column
            // widths are IDENTICAL to the row template's underneath (that is
            // what makes the columns line up), and its cells are labels rather
            // than bindings. Scope is deliberately narrow — only files that
            // already speak the grammar (they contain ReportTableHeaderStyle
            // somewhere), because the report WIZARD steps and the PI wizard use
            // their own header language (PiTableHeaderStyle / none) and
            // converting those is a redesign nobody asked for.
            foreach (var file in XamlFiles(Path.Combine("Views", "Reports")))
            {
                var text = File.ReadAllText(file);
                if (!text.Contains("ReportTableHeaderStyle", StringComparison.Ordinal)) continue;
                var doc = TryLoad(file);
                if (doc == null) continue;

                var bySignature = new Dictionary<string, List<XmlElement>>();
                foreach (XmlElement grid in doc.GetElementsByTagName("Grid"))
                {
                    string signature = Signature(grid);
                    if (signature.Length == 0) continue;
                    var labels = Descendants(grid)
                        .Where(t => t.LocalName == "TextBlock" && t.GetAttribute("Grid.Column").Length > 0)
                        .ToList();
                    if (labels.Count < 3) continue;
                    if (!bySignature.TryGetValue(signature, out var list))
                        bySignature[signature] = list = new List<XmlElement>();
                    list.Add(grid);
                }

                foreach (var pair in bySignature.Where(p => p.Value.Count > 1))
                {
                    foreach (var grid in pair.Value)
                    {
                        var labels = Descendants(grid)
                            .Where(t => t.LocalName == "TextBlock" && t.GetAttribute("Grid.Column").Length > 0)
                            .ToList();
                        bool isHeader = labels.All(t =>
                            !t.GetAttribute("Text").Contains("{Binding", StringComparison.Ordinal));
                        if (!isHeader) continue;

                        var band = grid.ParentNode as XmlElement;
                        Assert.True(band != null &&
                                    band.GetAttribute("Style").Contains("ReportTableHeaderStyle", StringComparison.Ordinal),
                            Path.GetFileName(file) + ": a " + labels.Count + "-column table header has " +
                            "no ReportTableHeaderStyle bar, so it does not match the " +
                            "Proforma hub header type the owner asked for.");
                        Assert.True(labels.All(t =>
                                t.GetAttribute("Style").Contains("ReportHeaderTextStyle", StringComparison.Ordinal)),
                            Path.GetFileName(file) + ": table-header labels must all carry " +
                            "ReportHeaderTextStyle (white bold), matching the bar.");
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  HELPERS
        // ────────────────────────────────────────────────────────────────

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Views", "Settings", "SettingsView.xaml")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("repository root not found");
        }

        private static IEnumerable<string> XamlFiles(string folder) =>
            Directory.EnumerateFiles(Path.Combine(Root(), folder), "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine("obj", string.Empty), StringComparison.Ordinal) &&
                            !f.Contains(Path.Combine("bin", string.Empty), StringComparison.Ordinal));

        private static IEnumerable<string> CSharpExportFiles()
        {
            foreach (var folder in new[] { Path.Combine("Services", "ProformaInvoice"), Path.Combine("Services", "Export") })
            {
                var path = Path.Combine(Root(), folder);
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
                    yield return file;
            }
        }

        private static XmlDocument? TryLoad(string path)
        {
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using var reader = XmlReader.Create(path, settings);
                var doc = new XmlDocument();
                doc.Load(reader);
                return doc;
            }
            catch (XmlException)
            {
                return null;   // a malformed file is the XAML compiler's problem, not this lock's
            }
        }

        private static IEnumerable<XmlElement> Children(XmlElement element) =>
            element.ChildNodes.OfType<XmlElement>();

        private static IEnumerable<XmlElement> Descendants(XmlElement element)
        {
            foreach (var child in Children(element))
            {
                yield return child;
                foreach (var deeper in Descendants(child)) yield return deeper;
            }
        }

        private static int TrackCount(XmlElement grid, string definitions, string track)
        {
            var block = Children(grid).FirstOrDefault(c => c.LocalName == definitions);
            return block == null ? 0 : Children(block).Count(c => c.LocalName == track);
        }

        private static int Index(XmlElement element, string attribute)
        {
            var raw = element.GetAttribute(attribute);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
        }

        private static string Signature(XmlElement grid)
        {
            var block = Children(grid).FirstOrDefault(c => c.LocalName == "Grid.ColumnDefinitions");
            if (block == null) return string.Empty;
            var widths = Children(block).Where(c => c.LocalName == "ColumnDefinition")
                .Select(c => c.GetAttribute("Width")).ToList();
            return widths.Count < 3 ? string.Empty : string.Join("|", widths);
        }

        private static string Offender(string file, XmlElement element) =>
            Path.GetFileName(file) + " <" + element.LocalName + ">";

        /// <summary>
        /// The chained call that starts at <paramref name="at"/> — follows the
        /// fluent chain (and any lambdas it opens) up to the terminating ';'.
        /// </summary>
        private static string ChainedCall(string code, int at)
        {
            int i = at, depth = 0;
            while (i < code.Length)
            {
                char c = code[i];
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ';' && depth == 0) break;
                i++;
            }
            return code.Substring(at, i - at);
        }

        private static string StripCommentsAndLiterals(string code)
        {
            var noBlock = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            var noLine = Regex.Replace(noBlock, @"//[^\n]*", string.Empty);
            return Regex.Replace(noLine, @"@?\$?""(?:""""|\\.|[^""\\])*""", "\"\"", RegexOptions.Singleline);
        }

        private static string LightTable(string source)
        {
            int at = source.IndexOf("Ios26GlobalLight()", StringComparison.Ordinal);
            Assert.True(at >= 0, "Ios26GlobalLight() not found");
            return ChainedCall(source, source.IndexOf('{', at));
        }

        private static string? RgbOf(string table, string key)
        {
            // Ios26Lin2 takes the light hex first; Ios26Sol takes (dark, light,
            // darkHex) — so the FIRST hex literal in the call is always the
            // light value this table publishes.
            var m = Regex.Match(table, @"\[""" + Regex.Escape(key) + @"""\]\s*=\s*Ios26\w+\([^""#]*""#([0-9A-Fa-f]{6,8})""");
            if (!m.Success) return null;
            var hex = m.Groups[1].Value;
            return hex.Length >= 6 ? hex.Substring(hex.Length - 6).ToUpperInvariant() : null;
        }

        private static int AlphaOf(string table, string key)
        {
            var m = Regex.Match(table, @"\[""" + Regex.Escape(key) + @"""\]\s*=\s*Ios26\w+\([^""#]*""#([0-9A-Fa-f]{8})""");
            return m.Success ? int.Parse(m.Groups[1].Value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0;
        }

        private static string Truncate(string value) =>
            value.Length <= 90 ? value : value.Substring(0, 90) + "…";
    }
}
