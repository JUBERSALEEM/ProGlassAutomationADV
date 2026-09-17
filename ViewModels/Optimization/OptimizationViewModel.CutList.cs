// ═══════════════════════════════════════════════════════════════════════
//  FILE: ViewModels/Optimization/OptimizationViewModel.CutList.cs
//  PROJECT: ProGlassAutomation
//
//  WHY THIS EXISTS
//  ---------------------------------------------------------------------
//  The engine could only be fed from a confirmed Job Order: tick a job on
//  the JOBS tab, import its Step 4 sheets, run. Anyone who just wanted to
//  optimize a list of sizes — no proforma, no job order, the way a normal
//  cutting-optimization app works — had no way in. The SIZES tab is a
//  read-only view of whatever pieces happen to exist, so it could not help.
//
//  This adds a CUT LIST tab: type or paste rows, send them to the engine,
//  add racks on STOCKS (which already accepts manual entry), and run.
//
//  WHY IT HOSTS Step 5's SpecGroup RATHER THAN A NEW GRID
//  The requested columns — sr no, glass ref, W1, H1, W2, H2, qty, m² of
//  W1×H1, m² of W2×H2, with qty/m²/m² autosums — are Step 5's grid exactly.
//  SpecGroup already implements all of it, including the cell-anchored Excel
//  paste that accepts a partial column set (just GLASS REF, just W1/H1, or
//  all six across) through IExcelPasteable + DataGridNavigationBehavior, and
//  it has a parameterless constructor whose only service is optional. So the
//  paste behaviour here is not a reimplementation that can drift from Step 5;
//  it IS Step 5's, and the m² figures come from the same SqmCalculator.
//
//  The coupling is deliberate and one-directional: this module already
//  depends on the proforma snapshot models and the production engine. A
//  second hand-written grid would have been ~1000 lines of duplicated,
//  separately-buggy paste and autosum logic.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel
    {
        // ── tab indexes ────────────────────────────────────────────────────
        //
        // Named so the three places in C# that jump to a tab after a run
        // (Phase15, the deep-link in the constructor, and Run) cannot silently
        // point at the wrong tab if the strip is ever reordered. The XAML rail
        // carries the same numbers as ConverterParameter / DataTrigger values.
        //
        // CUT LIST is deliberately the LAST index even though the rail shows it
        // second, straight after SIZES. Inserting it at index 2 would have
        // renumbered five existing tabs across seven XAML triggers and three C#
        // jump sites — every one of them a chance to make "open the report after
        // a run" land on the wrong tab, and none of it compilable here. Visual
        // order comes from element order in the rail, not from the index, so the
        // tab sits where it belongs with nothing else disturbed.

        /// <summary>JOBS — confirmed job orders.</summary>
        public const int TabIndexJobs = 0;

        /// <summary>SIZES — merged glass groups and the cut plan.</summary>
        public const int TabIndexSizes = 1;

        /// <summary>STOCKS — the racks to cut from.</summary>
        public const int TabIndexStocks = 2;

        /// <summary>SETTINGS — margins, kerf and effort.</summary>
        public const int TabIndexSettings = 3;

        /// <summary>SUMMARY — the run result.</summary>
        public const int TabIndexSummary = 4;

        /// <summary>2D VIEW — the sheets drawn to scale.</summary>
        public const int TabIndexTwoDView = 5;

        /// <summary>REPORT — export.</summary>
        public const int TabIndexReport = 6;

        /// <summary>CUT LIST — manual entry. Shown on the rail straight after
        /// SIZES; see the note above on why the index is last.</summary>
        public const int TabIndexCutList = 7;

        // ── the hosted grid ────────────────────────────────────────────────

        private SpecGroup? _manualCutList;

        /// <summary>
        /// The manual cut list. Created on first touch rather than in the
        /// constructor: a <see cref="SpecGroup"/> builds a collection view and
        /// hooks the spec catalogue, and the headless host a proforma quote
        /// drives its Step 5 runs through never opens this tab.
        /// </summary>
        public SpecGroup ManualCutList => _manualCutList ??= CreateManualCutList();

        private SpecGroup CreateManualCutList()
        {
            var group = new SpecGroup();

            // A build type up front. The field starts empty, and on a tab whose
            // whole purpose is "start here with nothing" an empty GLASS TYPE
            // dropdown reads as broken rather than as a choice not yet made. It
            // also drives the perimeter factor GENERATE applies to new rows.
            // Setting it here is side-effect free: the setter clears a spec that
            // belongs to a different family, and there is no spec yet.
            if (string.IsNullOrWhiteSpace(group.SelectedGlassType)
                && CutListGlassTypeOptions.Count > 0)
            {
                group.SelectedGlassType = CutListGlassTypeOptions[0];
            }

            return group;
        }

        /// <summary>Build types offered for the cut list, mirroring Step 5's
        /// <c>GlassTypeOptions</c> so the same vocabulary is used everywhere.
        /// Drives the perimeter factor the hosted grid applies to new rows.</summary>
        public List<string> CutListGlassTypeOptions { get; } =
            new() { "SGU", "DGU", "LAM", "DGU+LAM" };

        private string _cutListStatus = string.Empty;

        /// <summary>Outcome of the last cut-list action, shown under the grid.</summary>
        public string CutListStatus
        {
            get => _cutListStatus;
            private set
            {
                _cutListStatus = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCutListStatus));
            }
        }

        public bool HasCutListStatus => !string.IsNullOrWhiteSpace(_cutListStatus);

        private string _cutListRowCountText = "10";

        /// <summary>How many blank rows GENERATE adds.</summary>
        public string CutListRowCountText
        {
            get => _cutListRowCountText;
            set
            {
                if (_cutListRowCountText == value) return;
                _cutListRowCountText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        // ── commands ───────────────────────────────────────────────────────
        //
        // Created lazily on first binding, which keeps them out of the
        // constructor this view model already does database work in.
        // ADD ROW, REMOVE ROW and PASTE are bound straight to the hosted
        // SpecGroup's own commands in XAML — they are already correct there.

        private RelayCommand? _generateCutListRowsCommand;
        private RelayCommand? _clearCutListCommand;
        private RelayCommand? _sendCutListToEngineCommand;
        private RelayCommand? _openCutListTabCommand;

        /// <summary>Adds N blank rows, so a pasted block has somewhere to land.</summary>
        public ICommand GenerateCutListRowsCommand =>
            _generateCutListRowsCommand ??= new RelayCommand(_ => GenerateCutListRows());

        /// <summary>Empties the cut list. Does not touch the engine's pieces.</summary>
        public ICommand ClearCutListCommand =>
            _clearCutListCommand ??= new RelayCommand(_ => ClearCutList());

        /// <summary>Turns the cut list into the engine's piece list.</summary>
        public ICommand SendCutListToEngineCommand =>
            _sendCutListToEngineCommand ??= new RelayCommand(_ => SendCutListToEngine());

        /// <summary>Switches the rail to CUT LIST. Used by the button on the
        /// SIZES tab, so an operator looking at an empty cutting group can get
        /// to manual entry without hunting through the rail.</summary>
        public ICommand OpenCutListTabCommand =>
            _openCutListTabCommand ??= new RelayCommand(_ => SelectedTabIndex = TabIndexCutList);

        private void GenerateCutListRows()
        {
            int count = TextParsing.ParseInt(CutListRowCountText);
            if (count <= 0) count = 1;
            if (count > 5000) count = 5000;   // no unbounded queues

            ManualCutList.AddRows(count);
            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "{0} blank row(s) added — {1} row(s) in the list.",
                count, ManualCutList.ItemRows.Count);
        }

        private void ClearCutList()
        {
            int removed = ManualCutList.ItemRows.Count;
            ManualCutList.ItemRows.Clear();
            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "Cleared {0} row(s). The engine's piece list was left alone.", removed);
        }

        /// <summary>
        /// The composition every piece on this list cuts from.
        /// </summary>
        /// <remarks>
        /// Falls back step by step rather than failing: a typed spec name, then
        /// a picked spec, then the typed text on its own (the catalogue may be
        /// empty on a machine that never opened Step 5), then the build type,
        /// then a plain label. Something must always be there, because
        /// composition is what the engine groups pieces by and what decides
        /// which rack they cut from.
        /// </remarks>
        private string ResolveCutListComposition()
        {
            string spec = (ManualCutList.SelectedSpecName ?? string.Empty).Trim();
            if (spec.Length > 0) return spec;

            string picked = (ManualCutList.SelectedSpecItem?.SpecName ?? string.Empty).Trim();
            if (picked.Length > 0) return picked;

            string typed = (ManualCutList.SpecTypedText ?? string.Empty).Trim();
            if (typed.Length > 0) return typed;

            string glass = (ManualCutList.SelectedGlassType ?? string.Empty).Trim();
            if (glass.Length > 0) return glass;

            return "MANUAL GLASS";
        }

        /// <summary>
        /// Rebuilds the engine's piece list from the cut list.
        /// </summary>
        /// <remarks>
        /// One row can yield two pieces — W1×H1 as the outer pane and W2×H2 as
        /// the inner — which is the same routing Step 5 and the production
        /// engine use. A row with only W1/H1 yields one.
        ///
        /// REPLACES the piece list rather than appending, matching what the JOBS
        /// import does: one source owns the pieces at a time, so pressing SEND
        /// twice cannot double every piece. Racks on STOCKS are left exactly as
        /// the operator set them — that separation is the whole point of the tab.
        /// </remarks>
        private void SendCutListToEngine()
        {
            var rows = ManualCutList.ItemRows
                .Where(r => r != null && !r.IsEmpty && r.Qty > 0 && (r.W1 > 0 || r.W2 > 0))
                .ToList();

            if (rows.Count == 0)
            {
                CutListStatus = "Nothing to send yet — a row needs a quantity and at least "
                    + "one size (W1×H1, or W2×H2 for a second pane).";
                return;
            }

            string composition = ResolveCutListComposition();

            Pieces.Clear();
            int pieces = 0;
            int units = 0;
            int skipped = 0;

            foreach (var r in rows)
            {
                string label = string.IsNullOrWhiteSpace(r.GlassRef)
                    ? "—"
                    : r.GlassRef.Trim();
                string qty = r.Qty.ToString(CultureInfo.InvariantCulture);
                units += r.Qty;

                if (r.W1 > 0 && r.H1 > 0)
                {
                    var outer = NewPieceRow(composition, label,
                        r.W1.ToString(CultureInfo.InvariantCulture),
                        r.H1.ToString(CultureInfo.InvariantCulture), qty);
                    outer.Position = OuterPaneLabel;
                    Pieces.Add(outer);
                    pieces += r.Qty;
                }
                else
                {
                    skipped++;
                }

                if (r.W2 > 0 && r.H2 > 0)
                {
                    var inner = NewPieceRow(composition, label,
                        r.W2.ToString(CultureInfo.InvariantCulture),
                        r.H2.ToString(CultureInfo.InvariantCulture), qty);
                    inner.Position = InnerPaneLabel;
                    Pieces.Add(inner);
                    pieces += r.Qty;
                }
            }

            RefreshDerived();

            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "{0} row(s) → {1} piece(s) across {2} unit(s), cut from “{3}”. "
                + "Check the racks on STOCKS, then press RUN QUICK or RUN OPTIMIZE.",
                rows.Count, pieces, units, composition);

            if (skipped > 0)
            {
                CutListStatus += string.Format(CultureInfo.InvariantCulture,
                    " {0} row(s) had no W1×H1 and were sent as inner panes only.", skipped);
            }

            if (Stocks.Count == 0)
            {
                CutListStatus += " No racks on STOCKS yet — add the sheet sizes to cut from.";
            }
        }

        /// <summary>Pane labels used on the pieces this tab sends, matching the
        /// outer/inner vocabulary the rest of the chain uses.</summary>
        private const string OuterPaneLabel = "Outer";
        private const string InnerPaneLabel = "Inner";
    }
}
