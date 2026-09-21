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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.ProformaInvoice.Step5Grid;
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

            // ── PUBLISH BEFORE WIRING (2026-09-17 — crash fix) ────────────
            // The backing field MUST be assigned before anything below runs.
            // The two wiring steps raise change notifications synchronously:
            // the PropertyChanged hook re-raises CutListCompositionDisplay,
            // and the SelectedGlassType setter fires the moment it is given
            // the default build type. Every one of those notifications makes
            // the CUT LIST tab's bindings re-read this very property (the
            // title row binds CutListCompositionDisplay, whose getter reads
            // ManualCutList; the pane controls bind ManualCutList.* paths).
            // If the field were still null at that point — the old `??=`
            // assigned it only AFTER this method returned — each re-read
            // re-entered CreateManualCutList, which raised the notifications
            // again: unbounded re-entry, a StackOverflowException the moment
            // the Optimization module opened, and because a stack overflow
            // cannot be caught in .NET, no log line and no crash report.
            // Publishing first turns every re-entrant read into an immediate
            // return of the (mid-initialisation but already self-consistent)
            // instance, so the constructor can never nest.
            _manualCutList = group;

            // The title row shows what this list is made of from every pane, so
            // it has to follow the group's own edits — glass type, colours,
            // specification, sheet size — none of which go through this VM.
            group.PropertyChanged += (_, _) =>
                OnPropertyChanged(nameof(CutListCompositionDisplay));

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

            // Ensure specifications repository is wired from ServiceProvider so the dropdown is populated
            if (group.Specifications == null && App.ServiceProvider != null)
            {
                group.Specifications = App.ServiceProvider.GetService(
                    typeof(Services.ProformaInvoice.SpecificationsRepository.ISpecificationsRepository))
                    as Services.ProformaInvoice.SpecificationsRepository.ISpecificationsRepository;
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
            // Fresh rows have no result behind them; drop any stamp the
            // previous list left on the rows that are still there.
            foreach (GlassItemRow row in ManualCutList.ItemRows) row.ClearOptimizationStamp();
            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "{0} blank row(s) added — {1} row(s) in the list.",
                count, ManualCutList.ItemRows.Count);
        }

        private void ClearCutList()
        {
            int removed = ManualCutList.ItemRows.Count;
            foreach (GlassItemRow row in ManualCutList.ItemRows) row.ClearOptimizationStamp();
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
            // LostFocus cells do not commit until the cell loses focus. SEND
            // without tabbing out used to send an empty list.
            try
            {
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.FrameworkElement fe)
                    fe.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            }
            catch { }

            var rows = ManualCutList.ItemRows
                .Where(r => r != null && r.Qty > 0
                    && ((r.W1 > 0 && r.H1 > 0) || (r.W2 > 0 && r.H2 > 0)))
                .ToList();

            if (rows.Count == 0)
            {
                CutListStatus = "Nothing to send yet — a row needs a quantity and at least "
                    + "one size (W1×H1, or W2×H2 for a second pane).";
                return;
            }

            // Do not untick a confirmed JO. SEND packs W1/H1/W2/H2 as a
            // size-only run; store identity / leftover prices are not inputs.

            // Phase 2: canonical composition — the cut list's composition is
            // free text (a typed spec name or the build type), and it is the
            // merge key for cutting groups AND the rack-routing tag, so it
            // enters the module already canonicalised.
            string composition = CompositionKey.Normalize(ResolveCutListComposition());

            // PHASE 7 / finding 17 (2026-09-20): this used to open with
            //     Pieces.Clear();
            // — no confirmation, no undo, no merge. Tick jobs on the JOBS tab and
            // their panes auto-import into the cutting list; press SEND TO ENGINE
            // afterwards and that import was simply gone, replaced by whatever the
            // manual grid happened to hold.
            //
            // The fix is not a modal prompt. This module has NO dialog service at
            // all — it communicates through status lines, which is also what lets
            // the headless Step 5 host run without any UI, and injecting
            // IDialogService into a DI-resolved view model is the change the PI
            // wizard records as having broken construction once already (ERR-633EEB).
            // So the action itself stops being destructive: SEND now MERGES on the
            // same key the job auto-import uses — normalized composition + size +
            // position, summing quantity — and CLEAR CUT LIST stays the explicit,
            // separate way to start from nothing.

            int rowsBefore = Pieces.Count;
            int mergedRows = 0;

            var mergeIndex = new Dictionary<(string Comp, int W, int H, string Pos), PieceEntryVm>();
            foreach (var existing in Pieces)
            {
                if (existing == null || !existing.TryParse(out int ew, out int eh, out _)) continue;
                mergeIndex[(CompositionKey.Normalize(existing.GlassRef), ew, eh,
                    (existing.Position ?? string.Empty).Trim())] = existing;
            }

            int pieces = 0;
            int units = 0;
            int skipped = 0;

            // Adds a pane, or folds its quantity into the row already holding the
            // same glass at the same size and position. Merging may combine; it
            // must never make pieces disappear, so the summed quantity is written
            // back to the surviving row rather than replacing it.
            void AddOrMerge(string position, string label, int w, int h, int qty)
            {
                var key = (composition, w, h, (position ?? string.Empty).Trim());

                if (mergeIndex.TryGetValue(key, out var target) &&
                    target != null &&
                    target.TryParse(out _, out _, out int existingQty))
                {
                    target.QtyText = (existingQty + qty).ToString(CultureInfo.InvariantCulture);
                    mergedRows++;
                }
                else
                {
                    var row = NewPieceRow(composition, label,
                        w.ToString(CultureInfo.InvariantCulture),
                        h.ToString(CultureInfo.InvariantCulture),
                        qty.ToString(CultureInfo.InvariantCulture));
                    row.Position = position;
                    if (string.Equals(position, InnerPaneLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        row.W2 = w;
                        row.H2 = h;
                    }
                    else
                    {
                        row.W1 = w;
                        row.H1 = h;
                    }
                    mergeIndex[key] = row;
                    Pieces.Add(row);
                }

                pieces += qty;
            }

            foreach (var r in rows)
            {
                string label = string.IsNullOrWhiteSpace(r.GlassRef)
                    ? "—"
                    : r.GlassRef.Trim();
                units += r.Qty;

                if (r.W1 > 0 && r.H1 > 0)
                    AddOrMerge(OuterPaneLabel, label, r.W1, r.H1, r.Qty);
                else
                    skipped++;

                if (r.W2 > 0 && r.H2 > 0)
                    AddOrMerge(InnerPaneLabel, label, r.W2, r.H2, r.Qty);
            }

            _selectedCuttingGroup = AllCuttingGroups;
            EnsureSizeOnlyStock();
            RefreshDerived();

            _sizeOnlyPack = true;
            bool started;
            try
            {
                started = Run("QUICK");
            }
            finally
            {
                _sizeOnlyPack = false;
            }

            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "{0} row(s) → {1} piece(s) across {2} unit(s), cut from “{3}”. "
                + (started
                    ? "QUICK packing started from W1/H1/W2/H2."
                    : (string.IsNullOrWhiteSpace(RunMessage)
                        ? "Sizes are on the cutting list — add a rack on STOCKS if the run did not start."
                        : RunMessage)),
                rows.Count, pieces, units, composition);

            if (rowsBefore > 0)
            {
                CutListStatus += string.Format(CultureInfo.InvariantCulture,
                    " MERGED into the {0} row(s) already on the cutting list{1} — nothing was discarded. "
                    + "Use CLEAR CUT LIST first to replace them instead.",
                    rowsBefore,
                    mergedRows > 0
                        ? string.Format(CultureInfo.InvariantCulture,
                            " ({0} row(s) matched an existing size and had their quantity added)", mergedRows)
                        : string.Empty);
            }

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

        // ══════════════════════════════════════════════════════════════
        //  INNER WORKSPACE — SPECIFICATIONS · CUT LIST · RESULT
        //
        //  This tab exists for someone with no job order, so everything they
        //  must touch to complete a run lives here as a pane: build the
        //  specification, enter the sizes, read the result. The other
        //  top-panel tabs (STOCKS, SETTINGS, 2D VIEW, REPORT) belong to the
        //  module rather than to this list, so the same strip offers them as
        //  JUMPS instead of hosting a second copy of each view — duplicates
        //  would mean two live instances, double Loaded handlers and two sets
        //  of bindings over one view model, all to reach a tab that is one
        //  click away on the rail above.
        // ══════════════════════════════════════════════════════════════
        public const int CutListPaneSpecs = 0;
        public const int CutListPaneGrid = 1;
        public const int CutListPaneResult = 2;

        private int _cutListPaneIndex = CutListPaneGrid;

        /// <summary>
        /// Which pane of the cut-list workspace is showing. Defaults to the
        /// grid: that is what the tab is named after, and the specification
        /// pane is normally visited once per job.
        /// </summary>
        public int CutListPaneIndex
        {
            get => _cutListPaneIndex;
            set
            {
                int next = value < CutListPaneSpecs
                    ? CutListPaneSpecs
                    : value > CutListPaneResult ? CutListPaneResult : value;
                if (_cutListPaneIndex == next) return;
                _cutListPaneIndex = next;
                OnPropertyChanged();
            }
        }

        private RelayCommand? _jumpToOptimizationTabCommand;

        /// <summary>
        /// Moves the TOP rail to one of the module's global tabs from inside
        /// this workspace. An index outside the known range is ignored rather
        /// than selected, so a bad CommandParameter cannot park the module on
        /// a tab no view answers to.
        /// </summary>
        public ICommand JumpToOptimizationTabCommand =>
            _jumpToOptimizationTabCommand ??= new RelayCommand(parameter =>
            {
                int index = TextParsing.ParseInt(parameter as string);
                if (index < TabIndexJobs || index > TabIndexCutList) return;
                SelectedTabIndex = index;
            });

        /// <summary>
        /// One-line answer to "what is this list made of": the composition the
        /// engine will group by, plus the two pane colours. Read-only summary of
        /// the SPECIFICATIONS pane, kept on the title row so it stays visible
        /// while typing sizes — the composition decides which cutting group a
        /// piece lands in, which is not something to have to open a pane to see.
        /// </summary>
        public string CutListCompositionDisplay
        {
            get
            {
                string composition = ResolveCutListComposition();
                string outer = (ManualCutList.OuterGlassColor ?? string.Empty).Trim();
                string inner = (ManualCutList.InnerGlassColor ?? string.Empty).Trim();
                return string.Format(CultureInfo.InvariantCulture, "{0} · {1} / {2}",
                    composition,
                    outer.Length == 0 ? "no outer colour" : outer,
                    inner.Length == 0 ? "no inner colour" : inner);
            }
        }

        // ── specifications ────────────────────────────────────────────────
        //
        //  Only the three fields that have no home on the hosted SpecGroup are
        //  form properties here. Glass type, the two colours and the four sheet
        //  dimensions ARE the group's own state, so the pane binds them
        //  directly: editing a sheet size there changes what the engine cuts
        //  from, which is the point, and there is no second copy to fall out of
        //  sync.
        //
        //  Note the deliberate separation of two "thickness" ideas: the
        //  specification's ThicknessMm is glass thickness for the catalogue and
        //  for pricing, while the module's ThicknessText is a saw margin. They
        //  are not the same number and writing one from the other would corrupt
        //  the cutting margins.

        private string _cutListSpecName = string.Empty;
        public string CutListSpecName
        {
            get => _cutListSpecName;
            set
            {
                if (_cutListSpecName == value) return;
                _cutListSpecName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private string _cutListSpecPriceText = string.Empty;
        public string CutListSpecPriceText
        {
            get => _cutListSpecPriceText;
            set
            {
                if (_cutListSpecPriceText == value) return;
                _cutListSpecPriceText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private string _cutListSpecThicknessText = "6";
        public string CutListSpecThicknessText
        {
            get => _cutListSpecThicknessText;
            set
            {
                if (_cutListSpecThicknessText == value) return;
                _cutListSpecThicknessText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        private bool _isSavingCutListSpec;
        public bool IsSavingCutListSpec
        {
            get => _isSavingCutListSpec;
            private set
            {
                if (_isSavingCutListSpec == value) return;
                _isSavingCutListSpec = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveCutListSpec));
            }
        }

        /// <summary>
        /// Inverse of <see cref="IsSavingCutListSpec"/>, because the app has an
        /// inverse-bool-to-VISIBILITY converter and no inverse-bool converter —
        /// and reaching for a converter that does not exist is a runtime
        /// StaticResource failure, not a compile error.
        /// </summary>
        public bool CanSaveCutListSpec => !_isSavingCutListSpec;

        private RelayCommand? _saveCutListSpecCommand;

        /// <summary>
        /// Writes the specification to the shared catalogue, so a size list
        /// typed by hand stops being throwaway: the spec is then in the
        /// autocomplete in Step 4 and Step 5 too.
        /// </summary>
        public ICommand SaveCutListSpecCommand =>
            _saveCutListSpecCommand ??= new RelayCommand(_ => _ = SaveCutListSpecAsync());

        private async Task SaveCutListSpecAsync()
        {
            if (IsSavingCutListSpec) return;

            string glassType = (ManualCutList.SelectedGlassType ?? string.Empty).Trim();
            string specName = (CutListSpecName ?? string.Empty).Trim();

            if (glassType.Length == 0)
            {
                CutListStatus = "Pick a GLASS TYPE before saving a specification.";
                return;
            }

            if (specName.Length == 0)
            {
                CutListStatus = "A specification needs a name — it is what the catalogue and the cutting group are keyed on.";
                return;
            }

            var repository = ManualCutList.Specifications;
            if (repository == null)
            {
                CutListStatus = "The specification catalogue is not available here, so the spec can be used but not saved.";
                return;
            }

            int thickness = TextParsing.ParseInt(CutListSpecThicknessText);

            // PHASE 2 / finding 6 (2026-09-20): this used to be
            //     decimal.TryParse(TextParsing.CleanNum(CutListSpecPriceText), …) ? parsed : 0m;
            // CleanNum strips EVERY comma, so a European decimal comma turned
            // "45,5" into "455" — a tenfold price inflation written into the
            // specification catalogue, which Step 4 and Step 9 read back as a
            // fallback rate. And an unparseable value saved 0m with a status
            // line reporting success, so "free glass" entered the catalogue.
            //
            // A parse failure is an error, never a default, and an ambiguous
            // separator is refused rather than guessed: inflating a rate 10× is
            // the one mistake here that quietly reaches a customer's invoice.
            if (!TryParseMoney(CutListSpecPriceText, out decimal price) || price <= 0m)
            {
                CutListStatus = string.Format(CultureInfo.InvariantCulture,
                    "A specification needs a price per m² greater than zero — “{0}” was not read as one. " +
                    "Enter a plain number (45.5 or 45,5). Nothing was saved.",
                    (CutListSpecPriceText ?? string.Empty).Trim());
                return;
            }

            IsSavingCutListSpec = true;
            try
            {
                var saved = await repository.SaveOrUpdateAsync(
                    glassType,
                    specName,
                    price,
                    "MANUAL CUT LIST",
                    (ManualCutList.OuterGlassColor ?? string.Empty).Trim(),
                    thickness,
                    ManualCutList.OuterSheetWidthMm,
                    ManualCutList.OuterSheetHeightMm,
                    0,
                    null).ConfigureAwait(true);

                // Read it back into the grid straight away: the composition the
                // engine groups by, and the catalogue list the autocomplete
                // offers, both come from the group.
                ManualCutList.SelectedSpecName = saved?.SpecName ?? specName;
                await ManualCutList.LoadSpecsAsync().ConfigureAwait(true);

                CutListStatus = string.Format(CultureInfo.InvariantCulture,
                    "Saved specification “{0}” ({1}) to the catalogue at {2:0.##}/m².",
                    specName, glassType, price);
            }
            catch (Exception ex)
            {
                // Saving is gated on ProformaEdit and on read-only mode, so a
                // refusal here is a permission answer, not a crash. It belongs
                // on the pane's own status line, in the operator's face.
                CutListStatus = "Could not save the specification: " + ex.Message;
            }
            finally
            {
                IsSavingCutListSpec = false;
            }
        }

        // ── result read-back: QTY USED and OPT % per row ──────────────────
        //
        //  CostingRowVm already carries a join identity — WidthMm, HeightMm,
        //  PieceCount, UtilizationPercent — put there so Step 6 and Step 9 can
        //  read a run. This is the same join, from the other side: each cut-list
        //  row asks which results its two pane sizes landed in.

        private RelayCommand? _refreshCutListResultsCommand;

        /// <summary>Re-reads the last run into the grid's QTY USED / OPT % columns.</summary>
        public ICommand RefreshCutListResultsCommand =>
            _refreshCutListResultsCommand ??= new RelayCommand(_ => StampCutListResults());

        /// <summary>
        /// Stamps this tab's rows from the run currently on screen. Does nothing
        /// until a result exists, so an un-optimised list keeps showing "—"
        /// rather than zeros that read as "nothing fitted".
        /// </summary>
        public void StampCutListResults()
        {
            // Read the field, not the property: stamping must never be the
            // thing that constructs the hosted group.
            var rows = _manualCutList?.ItemRows;
            if (rows == null || rows.Count == 0) return;
            if (!HasResult) return;

            // Phase 3: scope the join to THIS list's composition. Every row
            // of one cut list cuts from one composition, so a result row from
            // a different glass group that happens to share a size must not
            // count toward this list's QTY USED.
            int stamped = StampRows(rows, CostingRows, ResolveCutListComposition());
            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "{0} of {1} row(s) matched the last run — QTY USED and OPT % are read from it.",
                stamped, rows.Count);
        }

        /// <summary>
        /// Joins rows to per-size results. Static and allocation-light so it can
        /// be tested without constructing this view model, whose constructor
        /// starts live database loads.
        /// </summary>
        /// <returns>How many rows found at least one matching result.</returns>
        public static int StampRows(
            IEnumerable<GlassItemRow>? rows,
            IEnumerable<CostingRowVm>? results,
            string? rowComposition = null)
        {
            if (rows == null || results == null) return 0;

            // Phase 3: when the caller knows the list's composition, result
            // rows from OTHER glass groups are excluded — the old size-only
            // join summed two groups' pieces whenever they shared a size
            // (low-e outer + another spec at 1057×2489 showed both totals on
            // every row).
            string rowComp = CompositionKey.Normalize(rowComposition);
            bool compScoped = rowComp.Length > 0;

            var bySize = new Dictionary<(int W, int H), (int Pieces, decimal Glass, decimal Stock)>();
            foreach (CostingRowVm? result in results)
            {
                if (result == null || result.WidthMm <= 0 || result.HeightMm <= 0) continue;
                if (compScoped && !string.Equals(
                        CompositionKey.Normalize(result.Composition), rowComp, StringComparison.Ordinal))
                    continue;
                var key = (result.WidthMm, result.HeightMm);
                bySize.TryGetValue(key, out (int Pieces, decimal Glass, decimal Stock) acc);
                bySize[key] = (acc.Pieces + result.PieceCount,
                               acc.Glass + result.GlassM2,
                               acc.Stock + result.StockM2);
            }

            if (bySize.Count == 0) return 0;

            int stamped = 0;
            foreach (GlassItemRow? row in rows)
            {
                if (row == null) continue;

                int pieces = 0;
                decimal glass = 0m, stock = 0m;
                bool matched = false;

                if (TrySize(bySize, row.W1, row.H1, out var outer))
                {
                    pieces += outer.Pieces; glass += outer.Glass; stock += outer.Stock;
                    matched = true;
                }

                if (TrySize(bySize, row.W2, row.H2, out var inner))
                {
                    pieces += inner.Pieces; glass += inner.Glass; stock += inner.Stock;
                    matched = true;
                }

                row.HasOptimizationResult = matched;
                row.QtyUsed = matched ? pieces : 0;
                // One number for the row, so it is the row's own yield: glass
                // over stock across both panes. Averaging the two percentages
                // would weight a small inner pane the same as a large outer.
                row.OptPercent = matched && stock > 0m
                    ? Math.Round(glass / stock * 100m, 2)
                    : 0m;

                if (matched) stamped++;
            }

            return stamped;
        }

        /// <summary>
        /// Size lookup that also accepts the rotated form. The engine places
        /// pieces rotated when that packs better, and a run's results are
        /// reported in the orientation it chose — so a 900×1900 row can come
        /// back as 1900×900 and still be the same glass.
        /// </summary>
        private static bool TrySize(
            Dictionary<(int W, int H), (int Pieces, decimal Glass, decimal Stock)> bySize,
            int w, int h,
            out (int Pieces, decimal Glass, decimal Stock) found)
        {
            found = default;
            if (w <= 0 || h <= 0) return false;
            if (bySize.TryGetValue((w, h), out found)) return true;
            return bySize.TryGetValue((h, w), out found);
        }

        /// <summary>Pane labels used on the pieces this tab sends, matching the
        /// outer/inner vocabulary the rest of the chain uses.</summary>
        private const string OuterPaneLabel = "Outer";
        private const string InnerPaneLabel = "Inner";

        /// <summary>
        /// Size packing needs a sheet envelope. Uses the cut-list outer/inner
        /// sheet when set; otherwise the 3210×2250 house rack. No price is
        /// invented.
        /// </summary>
        private void EnsureSizeOnlyStock()
        {
            foreach (var s in Stocks)
            {
                if (s == null || !s.IsUsedInRun) continue;
                if (TryParseWholeMm(s.WidthText, out int sw)
                    && TryParseWholeMm(s.HeightText, out int sh)
                    && sw > 0 && sh > 0)
                    return;
            }

            int w = 0, h = 0;
            if (_manualCutList != null)
            {
                if (_manualCutList.OuterSheetWidthMm > 0 && _manualCutList.OuterSheetHeightMm > 0)
                {
                    w = _manualCutList.OuterSheetWidthMm;
                    h = _manualCutList.OuterSheetHeightMm;
                }
                else if (_manualCutList.InnerSheetWidthMm > 0 && _manualCutList.InnerSheetHeightMm > 0)
                {
                    w = _manualCutList.InnerSheetWidthMm;
                    h = _manualCutList.InnerSheetHeightMm;
                }
            }
            if (w <= 0 || h <= 0)
            {
                w = 3210;
                h = 2250;
            }

            Stocks.Add(new StockEntryVm(
                w.ToString(CultureInfo.InvariantCulture),
                h.ToString(CultureInfo.InvariantCulture),
                string.Empty,
                "size-only rack")
            { IsUsedInRun = true });
            OnPropertyChanged(nameof(HasStocks));
        }

        /// <summary>
        /// Fills the CUT LIST tab from ticked jobs' Step 5 rows (GlassRef,
        /// W1, H1, W2, H2, Qty). Does not touch the grid when no job is
        /// ticked — operator-typed rows must survive an empty picker.
        /// </summary>
        private void SyncCutListFromSelectedJobs()
        {
            if (IsHeadlessHost) return;

            var jobs = AvailableJobs.Where(j => j.IsSelected).ToList();
            if (jobs.Count == 0) return;

            var import = new List<CutListImportRow>();
            foreach (var job in jobs)
            {
                if (job?.Step5Rows != null)
                {
                    foreach (var row in job.Step5Rows)
                    {
                        if (row == null) continue;
                        if (row.W1 <= 0 && row.H1 <= 0 && row.W2 <= 0 && row.H2 <= 0) continue;
                        import.Add(new CutListImportRow
                        {
                            GlassRef = row.GlassRef ?? string.Empty,
                            W1 = row.W1,
                            H1 = row.H1,
                            W2 = row.W2,
                            H2 = row.H2,
                            Qty = row.Qty > 0 ? row.Qty : 1
                        });
                    }
                }

                // If the embedded PI had no ItemRows, rebuild W1/H1/W2/H2
                // from the exploded pane layers so the studio grid is not empty.
                if (import.Count == 0 && job?.Specs != null)
                {
                    foreach (var spec in job.Specs)
                    {
                        foreach (var layer in spec.Layers.Where(l => l.IsPane))
                        {
                            bool inner = string.Equals(layer.Position, "Inner", StringComparison.OrdinalIgnoreCase)
                                || (layer.Position ?? string.Empty).IndexOf("Lam", StringComparison.OrdinalIgnoreCase) >= 0;
                            foreach (var piece in layer.Pieces)
                            {
                                if (piece.W <= 0 || piece.H <= 0) continue;
                                int qty = piece.Qty > 0 ? piece.Qty : 1;
                                import.Add(new CutListImportRow
                                {
                                    GlassRef = string.IsNullOrWhiteSpace(piece.Label) ? piece.Glass : piece.Label,
                                    W1 = inner ? 0 : piece.W,
                                    H1 = inner ? 0 : piece.H,
                                    W2 = inner ? piece.W : 0,
                                    H2 = inner ? piece.H : 0,
                                    Qty = qty
                                });
                            }
                        }
                    }
                }
            }

            var grid = ManualCutList;
            var primary = jobs[0];

            // Type then spec then colours/sheets. SelectedSpecName calls
            // AdoptSheetSelection which zeros sheets — write JO sheets after.
            if (!string.IsNullOrWhiteSpace(primary.GlassType))
                grid.SelectedGlassType = primary.GlassType;
            if (!string.IsNullOrWhiteSpace(primary.SpecName))
                grid.SelectedSpecName = primary.SpecName;
            grid.OuterGlassColor = primary.OuterGlassColor ?? string.Empty;
            grid.InnerGlassColor = primary.InnerGlassColor ?? string.Empty;
            grid.ThirdGlassColor = primary.ThirdGlassColor ?? string.Empty;
            grid.OuterSheetWidthMm = primary.OuterSheetWidthMm;
            grid.OuterSheetHeightMm = primary.OuterSheetHeightMm;
            grid.InnerSheetWidthMm = primary.InnerSheetWidthMm;
            grid.InnerSheetHeightMm = primary.InnerSheetHeightMm;
            grid.ThirdSheetWidthMm = primary.ThirdSheetWidthMm;
            grid.ThirdSheetHeightMm = primary.ThirdSheetHeightMm;
            grid.OuterSheetPricePerSqm = primary.OuterSheetPricePerSqm;
            grid.InnerSheetPricePerSqm = primary.InnerSheetPricePerSqm;
            OnPropertyChanged(nameof(CutListCompositionDisplay));

            if (import.Count == 0) return;

            grid.ItemRows.Clear();
            int added = grid.ApplyImportedRows(import);
            CutListStatus = string.Format(CultureInfo.InvariantCulture,
                "Imported {0} Step 5 row(s) from {1} ticked job(s) · {2} · {3}.",
                added, jobs.Count,
                string.IsNullOrWhiteSpace(primary.GlassType) ? "type unset" : primary.GlassType,
                string.IsNullOrWhiteSpace(primary.SpecName) ? "spec unset" : primary.SpecName);
        }
    }
}
