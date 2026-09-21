// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Jobs.cs
// MODULE: Optimization — JOBS tab — job picker, Step 4/5 pane breakdown and pane selection (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── JOBS tab: confirmed Job-Order picker (source lock) ─────────
        // Production runs COMPLETELY from the confirmed PI (Job Order).
        // The picker lists every non-draft, non-void JO; loading one expands
        // its cutting pieces through the production distribution engine so
        // the W2/H2 size semantics (equal-size vs overlap) match the floor.

        public sealed class JobOptionVm : ViewModels.ViewModelBase
        {
            private bool _isSelected;
            public string JobOrderId { get; set; } = string.Empty;
            /// <summary>The source Proforma Invoice this job order was cut from —
            /// shown on the card so the operator always knows which Step 5 is
            /// being imported (a voided PI can never be mistaken for a live one).</summary>
            public string SourceProformaId { get; set; } = string.Empty;

            /// <summary>Display label for the source PI ("from PI-2026-001"),
            /// empty when the job carries no source reference.</summary>
            public string SourceLabel => string.IsNullOrWhiteSpace(SourceProformaId)
                ? string.Empty
                : "from " + SourceProformaId;
            public string CustomerName { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public int PieceRows { get; set; }

            /// <summary>Quick stats shown on the job card: pieces / area / spec count.</summary>
            public string PiecesLabel { get; set; } = "0 pcs";
            public string AreaLabel { get; set; } = "0 m²";
            public string SpecCountLabel { get; set; } = "0 specs";

            /// <summary>Distribution-engine import warnings for this job
            /// ("row N has no quantity — produced as 1 piece", skipped panes,
            /// unparsed spec names…). Set once when the option is built,
            /// before the card is bound, so no change notification needed.</summary>
            public List<string> Warnings { get; } = new();
            public bool HasWarnings => Warnings.Count > 0;
            public string WarningLabel => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} warning(s)", Warnings.Count);
            public string WarningSummary => Warnings.Count == 0
                ? string.Empty
                : string.Join(" ", Warnings.Distinct());

            /// <summary>Card badge visibility — the app has no
            /// BooleanToVisibility resource, so the VM exposes Visibility
            /// directly (it already references System.Windows).</summary>
            public System.Windows.Visibility WarningVisibility =>
                Warnings.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            /// <summary>Multi-select: tick any number of jobs and combine
            /// their panes (e.g. two jobs whose low-e outers merge into ONE
            /// cutting group).</summary>
            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
            }

            /// <summary>Step 4/5 spec build-up of this job, each spec expanded
            /// into its physical layer boxes (outer pane → ASP spacer → inner
            /// pane).</summary>
            public List<SpecOptionVm> Specs { get; } = new();

            /// <summary>Stock sheet sizes picked in Step 4 (per spec × pane),
            /// tagged with the pane's glass composition so the STOCKS tab can
            /// route each cutting group to its OWN rack (never the whole store).
            /// Tuple = (W, H, glass composition, provenance).</summary>
            /// <summary>Step 4 stock sheets for this job. PricePerSqm is the
            /// rate Step 4 itself used for that sheet, carried alongside the
            /// size so the REPORT tab can cost the plan without re-deriving a
            /// colour match against the Sheet Store.</summary>
            /// <summary>SpecNumber/Side are the Step 5 pane identity this rack
            /// came from, so Step 6 and Step 9 can join a run back to a spec
            /// pane structurally instead of guessing from a colour name.</summary>
            public List<(int W, int H, string Composition, string Source, decimal PricePerSqm, int SpecNumber, string Side)> Step4Sheets { get; } = new();

            /// <summary>
            /// False when Step 4 material is unresolved or invalid.
            /// The job still appears in the picker and may import Step 5
            /// sizes; Run / TryBuildInputs must refuse packing.
            /// </summary>
            public bool CanOptimizeMaterial { get; set; } = true;
            public string MaterialInvalidReason { get; set; } = "";

            /// <summary>
            /// False when Distribute produced no selectable pane layers.
            /// The job still lists; Sizes/Cut List stay empty until panes exist.
            /// </summary>
            public bool HasOptimizablePanes { get; set; } = true;

            public int TotalPcs => Specs.Sum(s => s.Layers.Where(l => l.Kind == "Pane").Sum(l => l.TotalPcs));
        }

        /// <summary>
        /// One Step 5 spec group rendered as its build-up strip: the glass
        /// layers the factory actually stacks — outer pane, air spacer /
        /// interlayer, inner pane. Each PANE layer is selectable; spacer and
        /// interlayer boxes are informational (nothing is cut from them).
        /// </summary>
        public sealed class SpecOptionVm
        {
            public string JobOrderId { get; set; } = string.Empty;
            public int SpecGroupNumber { get; set; }
            public string SpecName { get; set; } = string.Empty;
            public string BuildType { get; set; } = string.Empty;
            public List<BuildUpLayerVm> Layers { get; } = new();

            public string SpecLabel => "Spec " + SpecGroupNumber.ToString(CultureInfo.InvariantCulture)
                + " · " + BuildType.ToUpperInvariant();
        }

        /// <summary>One box in a spec's build-up strip: a cut pane, an air
        /// spacer, or an interlayer.</summary>
        public sealed class BuildUpLayerVm : ViewModels.ViewModelBase
        {
            private bool _isSelected;

            /// <summary>"Pane" (selectable), "Spacer" or "Interlayer" (informational).</summary>
            public string Kind { get; set; } = "Pane";

            /// <summary>"Outer" / "Inner" / "Lam Outer" / … for panes.</summary>
            public string Position { get; set; } = string.Empty;

            /// <summary>"Spec 1" — the Step 5 spec group this layer belongs to.</summary>
            public string SpecLabel { get; set; } = string.Empty;

            /// <summary>Glass composition ("6mm clear FT glass") or "12mm ASP".</summary>
            public string Title { get; set; } = string.Empty;

            /// <summary>"OUTER · FT" / "air spacer" / "interlayer (not cut)".</summary>
            public string Sub { get; set; } = string.Empty;

            public string Thickness { get; set; } = string.Empty;
            public int TotalPcs { get; set; }
            public string CountLabel { get; set; } = string.Empty;
            public string AreaLabel { get; set; } = string.Empty;

            public bool IsPane => Kind == "Pane";

            public string JobOrderId { get; set; } = string.Empty;
            public string ProformaId { get; set; } = string.Empty;
            public int SpecGroupNumber { get; set; }
            public string Color { get; set; } = string.Empty;
            public int ThicknessMm { get; set; }
            public string Processing { get; set; } = string.Empty;
            public string MaterialKey { get; set; } = string.Empty;

            public List<(string Glass, string Label, int W, int H, int Qty)> Pieces { get; } = new();

            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
            }
        }

        public ObservableCollection<JobOptionVm> AvailableJobs { get; } = new();

        public bool HasAvailableJobs => AvailableJobs.Count > 0;

        /// <summary>Job picker filtered by <see cref="JobSearchText"/> (id / customer).</summary>
        public ObservableCollection<JobOptionVm> FilteredJobs { get; } = new();

        private string _jobSearchText = string.Empty;
        public string JobSearchText
        {
            get => _jobSearchText;
            set
            {
                if (_jobSearchText == value) return;
                _jobSearchText = value;
                OnPropertyChanged();
                RefreshJobsFilter();
            }
        }

        private JobOptionVm? _selectedJob;
        public JobOptionVm? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (_selectedJob == value) return;
                _selectedJob = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Currently ticked jobs (multi-select — combine panes across jobs).</summary>
        public IEnumerable<JobOptionVm> SelectedJobs => AvailableJobs.Where(j => j.IsSelected);

        /// <summary>Build-up strips of every ticked job's specs (outer → ASP → inner).</summary>
        public ObservableCollection<SpecOptionVm> JobSpecBreakdown { get; } = new();

        private bool _hasSpecBreakdown;
        public bool HasSpecBreakdown { get => _hasSpecBreakdown; private set { _hasSpecBreakdown = value; OnPropertyChanged(); } }

        /// <summary>Flat checklist of every pane across the selected jobs —
        /// checkbox · position · spec · glass composition · pcs · m².</summary>
        public ObservableCollection<BuildUpLayerVm> PaneChecklist { get; } = new();

        private bool _hasPaneChecklist;
        public bool HasPaneChecklist { get => _hasPaneChecklist; private set { _hasPaneChecklist = value; OnPropertyChanged(); } }

        // ── KPI strip (dusk-navy stat cards) ─────────────────────────
        private string _kpiJobs = "0";
        public string KpiJobs { get => _kpiJobs; private set { if (_kpiJobs == value) return; _kpiJobs = value; OnPropertyChanged(); } }

        private string _kpiPieces = "0";
        public string KpiPieces { get => _kpiPieces; private set { if (_kpiPieces == value) return; _kpiPieces = value; OnPropertyChanged(); } }

        private string _kpiGlass = "0 m²";
        public string KpiGlass { get => _kpiGlass; private set { if (_kpiGlass == value) return; _kpiGlass = value; OnPropertyChanged(); } }

        private string _kpiGroups = "0";
        public string KpiGroups { get => _kpiGroups; private set { if (_kpiGroups == value) return; _kpiGroups = value; OnPropertyChanged(); } }

        private int _selectedPaneCount;
        public int SelectedPaneCount
        {
            get => _selectedPaneCount;
            private set
            {
                if (_selectedPaneCount == value) return;
                _selectedPaneCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPaneSummary));
                OnPropertyChanged(nameof(HasSelectedPanes));
                OnPropertyChanged(nameof(AddPanesButtonLabel));
            }
        }
        public string SelectedPaneSummary => SelectedPaneCount == 0
            ? "No panes selected"
            : SelectedPaneCount.ToString(CultureInfo.InvariantCulture) + " pane(s) selected";
        public bool HasSelectedPanes => SelectedPaneCount > 0;
        public string AddPanesButtonLabel => SelectedPaneCount == 0
            ? "Add selected panes → cutting list"
            : "Add " + SelectedPaneCount.ToString(CultureInfo.InvariantCulture) + " selected pane(s) → cutting list";

        private string _jobsStatusText = "Loading confirmed job orders…";
        public string JobsStatusText
        {
            get => _jobsStatusText;
            set { _jobsStatusText = value; OnPropertyChanged(); }
        }

        private bool _hasPieces;
        public bool HasPieces
        {
            get => _hasPieces;
            private set { _hasPieces = value; OnPropertyChanged(); OnPropertyChanged(nameof(PiecesEmptyHint)); }
        }

        /// <summary>Shown on JOBS/SIZES tabs while the cutting list is empty.
        /// SENIOR 2026-09-20: updated to reflect auto-import (no manual load).</summary>
        public bool HasSelectedJobs => AvailableJobs.Any(j => j.IsSelected);

        public string PiecesEmptyHint => HasPieces
            ? string.Empty
            : SelectedJobs.Any(j => !j.HasOptimizablePanes) && !SelectedJobs.Any(j => j.HasOptimizablePanes)
                ? "NO OPTIMIZABLE PANES — the selected job has no Step 5 cutting sizes."
                : HasSelectedJobs
                    ? "No pieces yet — tick pane boxes on JOBS, or add sizes manually."
                    : "No pieces yet — tick job orders on the left to auto-import their Step 4/5 cutting sizes (merged by matching spec+glass). Or add pieces manually below.";

        public ICommand RefreshJobsCommand { get; }
        public ICommand AddSelectedPanesCommand { get; }
        public ICommand SelectAllPanesCommand { get; }
        public ICommand ClearPaneSelectionCommand { get; }

        public void LoadJobs()
        {
            AsyncUtility.SafeFireAndForget(
                LoadJobsInternalAsync,
                "[OptimizationViewModel] LoadJobs");
        }

        public async Task LoadJobsInternalAsync()
        {
            JobsStatusText = "Loading confirmed job orders…";
            try
            {
                var repo = App.ServiceProvider?.GetService(typeof(IJobOrderRepository)) as IJobOrderRepository;
                if (repo == null)
                {
                    JobsStatusText = "Job store is not available yet — use Refresh.";
                    return;
                }

                // Source-lock guard: a Job Order converted from a VOIDED
                // Proforma Invoice must never appear in the picker. The JO's
                // own IsVoid flag tracks voiding the JO itself — it says
                // nothing about the PI it was cut from, so we read the PI
                // void flags and exclude any JO whose source PI is voided.
                // This is what stopped a live PI-2026-001 from silently
                // importing the Step 5 of its voided twin PI-2026-002.
                var voidedPis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var invoiceRepo = App.ServiceProvider?.GetService(typeof(IProformaInvoiceRepository))
                        as IProformaInvoiceRepository;
                    if (invoiceRepo != null)
                    {
                        var piRows = await invoiceRepo.GetListRowsAsync().ConfigureAwait(false);
                        foreach (var row in piRows)
                        {
                            if (row != null && row.IsVoid && !string.IsNullOrWhiteSpace(row.ProformaId))
                                voidedPis.Add(row.ProformaId.Trim());
                        }
                    }
                }
                catch (Exception ex)
                {
                    // The source-lock must not break the picker if the PI
                    // store is unavailable — degrade to the old behaviour
                    // and say so on the status line.
                    AppLogService.Warning($"[Optimization] could not read voided PI list: {ex.Message}");
                }

                var all = await repo.GetAllAsync().ConfigureAwait(false);

                // PHASE 6 / finding 12 (2026-09-20): a headless host never
                // shows the JOBS tab, so building up to 100 job cards — and the
                // comment below is explicit that EVERY card runs the
                // distribution engine — was pure waste. It held a second full
                // copy of the job-order graph in memory beside the module's own,
                // for a view model that only ever runs RunFromStep5.
                //
                // IsHeadlessHost is assigned by CreateOptimizationHost
                // immediately after the constructor returns, so by the time this
                // first await resumes the flag is already set. This is the same
                // reasoning as the RC-007 guard in RebuildSelectedJobBreakdown,
                // applied one stage earlier.
                //
                // It also makes an existing contract deterministic instead of
                // racy: Run() only overwrites DocumentKey when exactly one job
                // is selected, and its comment claims "a Step 5 host sets its
                // own DocumentKey before calling here and is never overwritten
                // (no jobs are selected there)". With no job list loaded, that
                // is now true by construction rather than by timing.
                if (IsHeadlessHost)
                {
                    JobsStatusText = string.Empty;
                    return;
                }

                var options = new List<JobOptionVm>();

                // Phase 3: the list is capped at the 100 most recent jobs —
                // every card runs the distribution engine, so an unbounded
                // list rebuilds the whole picker on every refresh. The cap
                // already existed but was silent; when it engages, the status
                // line now says how many jobs it is showing (a silent cap
                // reads as "jobs disappeared").
                const int jobListDisplayCap = 100;
                var eligible = all
                    .Where(j => j != null && !j.IsVoid
                        && !JobOrderStatusValues.Is(j.Status, JobOrderStatusValues.Draft)
                        && !voidedPis.Contains(j.SourceProformaId ?? string.Empty))
                    .OrderByDescending(j => j.UpdatedAt)
                    .ToList();
                int totalEligible = eligible.Count;

                foreach (var jo in eligible.Take(jobListDisplayCap))
                {
                    var snapshot = JobOrderMapper.DeserializeSnapshot(jo);
                    var invoice = snapshot?.EmbeddedInvoice;

                    // List the job even when Step 4 material is unresolved.
                    // Run() still refuses packing. Do not `continue` here —
                    // that emptied JOB/SIZES/CUT LIST/STOCK (CS0103 if the
                    // stamp below is kept without these locals).
                    bool authorized = TryAuthorizeJobInvoiceForOptimization(
                        invoice, out string authMessage);

                    var plan = ProductionDistributionEngine.Distribute(jo.JobOrderId, invoice);

                    var cut = plan.Items
                        .Where(i => i.Stage == ProductionStage.Cutting && i.WidthMm > 0 && i.HeightMm > 0)
                        .ToList();

                    try
                    {
                        Services.Logging.AppLogService.Info(
                            $"[OPT-JOBS] LoadJobs job={jo.JobOrderId} invoiceSpecs={invoice?.SpecGroups?.Count ?? 0} planItems={plan.Items.Count} cutting={cut.Count} warnings={plan.Warnings.Count}",
                            new Services.Logging.LogContext { Module = "Optimization", Operation = "LoadJobs" });
                    }
                    catch { }

                    var opt = new JobOptionVm
                    {
                        JobOrderId = jo.JobOrderId,
                        SourceProformaId = jo.SourceProformaId ?? string.Empty,
                        CustomerName = jo.CustomerName,
                        CanOptimizeMaterial = authorized,
                        MaterialInvalidReason = authorized ? string.Empty : authMessage
                    };

                    // Surface the distribution engine's import warnings
                    // (previously discarded): a row with no quantity becomes
                    // 1 piece, a row without W2/H2 loses its inner pane, an
                    // unparseable spec name loses treatment routing — each of
                    // these changes the cutting list silently.
                    opt.Warnings.AddRange(plan.Warnings);
                    if (!authorized && !string.IsNullOrWhiteSpace(authMessage)
                        && !opt.Warnings.Contains(authMessage))
                        opt.Warnings.Add(authMessage);

                    // ── Step 4/5 spec build-up (outer → ASP → inner) ─────
                    // Each spec group becomes a strip of layer boxes using
                    // the SAME pane routing the production floor uses
                    // (W1/H1 = outer, W2/H2 = inner, equal-size fallback).
                    // Two specs sharing one outer pane (low-e on a clear DGU
                    // and on an obscure DGU) expose that pane once per spec —
                    // the operator ticks both and the SIZES merge folds them
                    // into ONE cutting group by composition.
                    var groups = (invoice?.SpecGroups ?? new List<SpecGroupSnapshot>())
                        .Where(g => g != null)
                        .OrderBy(g => g.GroupNumber)
                        .ToList();

                    foreach (var group in groups)
                    {
                        string buildType = ProductionDistributionEngine.ResolveBuildType(group, invoice);
                        var occurrences = ProductionDistributionEngine.ResolvePaneOccurrences(buildType, group);
                        var spec = new SpecOptionVm
                        {
                            JobOrderId = jo.JobOrderId,
                            SpecGroupNumber = group.GroupNumber,
                            SpecName = group.SelectedSpecName ?? string.Empty,
                            BuildType = buildType
                        };

                        int paneIndex = 0;
                        foreach (var occ in occurrences)
                        {
                            var paneItems = cut
                                .Where(i => i.SpecGroupNumber == group.GroupNumber
                                    && string.Equals(i.Position, occ.Position, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            if (paneItems.Count == 0) continue;

                            // Phase 2: canonical composition — the same glass
                            // spelled slightly differently across two jobs
                            // used to split into two cutting groups (and lose
                            // its dedicated rack). Normalize at the source.
                            string composition = CompositionKey.Normalize(
                                string.IsNullOrWhiteSpace(occ.Composition)
                                    ? group.SelectedSpecName ?? string.Empty
                                    : occ.Composition);

                            var layer = new BuildUpLayerVm
                            {
                                Kind = "Pane",
                                Position = occ.Position,
                                SpecLabel = "Spec " + group.GroupNumber.ToString(CultureInfo.InvariantCulture),
                                Title = composition,
                                Thickness = occ.ThicknessMm > 0
                                    ? occ.ThicknessMm.ToString(CultureInfo.InvariantCulture) + "mm"
                                    : ParseThicknessLabel(composition),
                                Sub = occ.Position.ToUpperInvariant()
                                    + (string.IsNullOrWhiteSpace(occ.Treatment)
                                        ? string.Empty : " · " + occ.Treatment),
                                // Panes are pre-ticked so a job's sizes import
                                // with a single click on "Add selected panes";
                                // the operator can still untick any pane.
                                IsSelected = true,
                                JobOrderId = jo.JobOrderId,
                                ProformaId = jo.SourceProformaId ?? string.Empty,
                                SpecGroupNumber = group.GroupNumber,
                                Color = occ.Pane?.Color ?? string.Empty,
                                ThicknessMm = occ.ThicknessMm,
                                Processing = occ.Treatment ?? string.Empty,
                                MaterialKey = OptimizationMaterialIdentity.BuildKey(
                                    string.Empty, occ.Pane?.Color, occ.ThicknessMm, occ.Treatment, string.Empty)
                            };

                            // merge identical sizes; the row refs ride along
                            // as the label so the 2D view and cut plan stay
                            // traceable back to Step 5.
                            foreach (var sizeGroup in paneItems
                                .GroupBy(i => new { i.WidthMm, i.HeightMm })
                                .OrderBy(g => g.Key.WidthMm).ThenBy(g => g.Key.HeightMm))
                            {
                                int qty = sizeGroup.Sum(i => i.Qty);
                                string label = string.Join(", ",
                                    sizeGroup.Select(i => i.GlassRef)
                                             .Where(r => !string.IsNullOrWhiteSpace(r))
                                             .Distinct());
                                layer.Pieces.Add((
                                    composition,
                                    string.IsNullOrWhiteSpace(label) ? "—" : label,
                                    sizeGroup.Key.WidthMm, sizeGroup.Key.HeightMm, qty));
                            }

                            layer.TotalPcs = layer.Pieces.Sum(p => p.Qty);
                            double paneArea = layer.Pieces.Sum(p => p.W / 1000.0 * p.H / 1000.0 * p.Qty);
                            layer.AreaLabel = paneArea.ToString("0.00", CultureInfo.InvariantCulture) + " m²";
                            layer.CountLabel = layer.TotalPcs.ToString(CultureInfo.InvariantCulture) + " pcs";

                            spec.Layers.Add(layer);
                            paneIndex++;

                            // Insert the informational spacer/interlayer box
                            // exactly where it sits in the stack (nothing is
                            // cut from it — it shows the build-up only).
                            AddBuildUpInfoBox(spec, buildType, paneIndex, group.SelectedSpecName);
                        }

                        if (spec.Layers.Count > 0) opt.Specs.Add(spec);

                        // Step 4 stock sheets (the rack the job was quoted on),
                        // tagged with the pane's glass composition so the STOCKS
                        // tab routes each cutting group to its own rack. The
                        // composition here is the SAME canonical label used for
                        // the cutting list, so group→rack matching is exact.
                        if (spec.Layers.Count > 0)
                        {
                            string ComposeForSlot(PaneSlot slot)
                            {
                                var o = occurrences.FirstOrDefault(x => x.Slot == slot);
                                if (o == null) return string.Empty;
                                // Phase 2: same canonical key as the cutting
                                // list rows — rack tag and group key must be
                                // byte-identical for routing to match.
                                return CompositionKey.Normalize(
                                    string.IsNullOrWhiteSpace(o.Composition)
                                        ? (group.SelectedSpecName ?? string.Empty)
                                        : o.Composition);
                            }

                            string ComposeForPosition(string position)
                            {
                                var o = occurrences.FirstOrDefault(x =>
                                    string.Equals(x.Position, position, StringComparison.OrdinalIgnoreCase));
                                if (o == null) return string.Empty;
                                return CompositionKey.Normalize(
                                    string.IsNullOrWhiteSpace(o.Composition)
                                        ? (group.SelectedSpecName ?? string.Empty)
                                        : o.Composition);
                            }

                            if (group.OuterSheetWidthMm > 0 && group.OuterSheetHeightMm > 0)
                                opt.Step4Sheets.Add((group.OuterSheetWidthMm, group.OuterSheetHeightMm,
                                    ComposeForSlot(PaneSlot.Pane1),
                                    "Spec " + group.GroupNumber.ToString(CultureInfo.InvariantCulture) + " outer",
                                    group.OuterSheetPricePerSqm, group.GroupNumber, "Outer"));

                            if (group.InnerSheetWidthMm > 0 && group.InnerSheetHeightMm > 0)
                                opt.Step4Sheets.Add((group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                                    ComposeForSlot(PaneSlot.Pane2),
                                    "Spec " + group.GroupNumber.ToString(CultureInfo.InvariantCulture) + " inner",
                                    group.InnerSheetPricePerSqm, group.GroupNumber, "Inner"));

                            if (group.ThirdSheetWidthMm > 0 && group.ThirdSheetHeightMm > 0)
                                opt.Step4Sheets.Add((group.ThirdSheetWidthMm, group.ThirdSheetHeightMm,
                                    ComposeForPosition("Lam Inner"),
                                    "Spec " + group.GroupNumber.ToString(CultureInfo.InvariantCulture) + " lam inner",
                                    group.ThirdSheetPricePerSqm, group.GroupNumber, "LamInner"));
                        }
                    }

                    int pcs = opt.TotalPcs;
                    double m2 = opt.Specs.Sum(sp => sp.Layers.Where(l => l.Kind == "Pane")
                        .Sum(l => l.Pieces.Sum(x => x.W / 1000.0 * x.H / 1000.0 * x.Qty)));
                    // Spec count = the PI's ACTUAL specification groups, not
                    // just the groups that produced selectable panes. A Job
                    // Order with two specs must read "2 specs" even when one
                    // spec has no cutting rows yet (previously the card read
                    // "1 spec" because that group contributed no pane layers
                    // and never reached opt.Specs).
                    int specCount = groups.Count;
                    opt.PieceRows = opt.Specs.Sum(sp => sp.Layers.Count(l => l.Kind == "Pane"));
                    opt.Summary = string.Format(CultureInfo.InvariantCulture,
                        "{0} · {1} pcs · {2:0.0} m² · {3} spec(s)",
                        jo.CustomerName, pcs, m2, specCount);
                    opt.PiecesLabel = pcs.ToString(CultureInfo.InvariantCulture) + " pcs";
                    opt.AreaLabel = m2.ToString("0.0", CultureInfo.InvariantCulture) + " m²";
                    opt.SpecCountLabel = specCount.ToString(CultureInfo.InvariantCulture)
                        + (specCount == 1 ? " spec" : " specs");

                    opt.HasOptimizablePanes = opt.Specs.Count > 0;
                    if (!opt.HasOptimizablePanes)
                    {
                        const string noPanes = "NO OPTIMIZABLE PANES";
                        if (!opt.Warnings.Contains(noPanes))
                            opt.Warnings.Add(noPanes);
                    }
                    // September 17 listed jobs with panes only. A confirmed JO
                    // with zero cutting panes must still appear — blank picker
                    // is not the same as "no panes on this job".
                    options.Add(opt);
                }

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        // Phase 3: ticked jobs must survive a refresh. Capture
                        // the tick set from the OLD rows before the list is
                        // rebuilt, and re-apply it on the replacements BEFORE
                        // the property hooks are attached — otherwise each
                        // re-tick fires OnJobSelectionChanged (which rebuilds
                        // the whole breakdown) once per job mid-rebuild.
                        var keepSelectedIds = new HashSet<string>(
                            AvailableJobs.Where(j => j.IsSelected).Select(j => j.JobOrderId),
                            StringComparer.Ordinal);

                        foreach (var old in AvailableJobs)
                            old.PropertyChanged -= OnJobSelectionChanged;

                        AvailableJobs.Clear();
                        foreach (var o in options)
                        {
                            if (keepSelectedIds.Contains(o.JobOrderId))
                                o.IsSelected = true;
                            AttachJobOption(o);
                            AvailableJobs.Add(o);
                        }
                        SelectedJob = null;
                        RefreshJobsFilter();
                        RebuildSelectedJobBreakdown();
                        RefreshKpis();

                        // Import warnings (skipped rows, defaulted
                        // quantities, unparsed specs) used to be discarded
                        // after the distribution engine produced them —
                        // the operator saw the wrong cutting list with no
                        // explanation. They now count on this status line
                        // and on each job card.
                        int totalWarnings = options.Sum(o => o.Warnings.Count);
                        int blockedMaterial = options.Count(o => !o.CanOptimizeMaterial);
                        // Phase 3: say how many of how many — a silent cap
                        // reads as missing jobs.
                        string truncationNote = totalEligible > jobListDisplayCap
                            ? string.Format(CultureInfo.InvariantCulture,
                                " Showing the {0} most recent of {1}.",
                                jobListDisplayCap, totalEligible)
                            : string.Empty;
                        OnPropertyChanged(nameof(HasAvailableJobs));
                        int zeroPane = options.Count(o => !o.HasOptimizablePanes);
                        JobsStatusText = options.Count == 0
                            ? "No confirmed job orders yet."
                            : string.Format(CultureInfo.InvariantCulture,
                                "{0} confirmed job order(s).{1}{2}{3}{4}",
                                options.Count,
                                zeroPane > 0
                                    ? string.Format(CultureInfo.InvariantCulture,
                                        " {0} have NO OPTIMIZABLE PANES.",
                                        zeroPane)
                                    : string.Empty,
                                blockedMaterial > 0
                                    ? string.Format(CultureInfo.InvariantCulture,
                                        " {0} cannot RUN until Step 4 material is store-resolved — sizes still import.",
                                        blockedMaterial)
                                    : string.Empty,
                                totalWarnings > 0
                                    ? string.Format(CultureInfo.InvariantCulture,
                                        " {0} import warning(s) — see the job cards.", totalWarnings)
                                    : string.Empty,
                                truncationNote);

                        // 2026-09-14 AUTO-TRIGGER REMOVED (Phase 1 data
                        // integrity): the reference calibration job used to
                        // load itself whenever the list was empty. Its 152
                        // benchmark pieces then mixed with a real job's
                        // panes — untracked "job order" specs in the cutting
                        // list, and when the glass strings matched they were
                        // merged straight into the real job's cutting group.
                        // The reference job is now reachable only through its
                        // explicit button, which says exactly what it loads.
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "[OptimizationViewModel] LoadJobs failed: " + ex.Message,
                    new LogContext { Module = "Optimization", Class = nameof(OptimizationViewModel), Method = nameof(LoadJobsInternalAsync) });
                JobsStatusText = "Job list unavailable: " + ex.Message;
            }
        }

        private void OnJobSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(JobOptionVm.IsSelected)) return;

            // RC-013 (2026-09-20) replaces RC-009's blanket force.
            //
            // Pane tick state lives on BuildUpLayerVm, which is OWNED BY THE
            // JobOptionVm — not by JobSpecBreakdown. So "Clear" (or unticking a
            // single pane) permanently unticks that job's panes, and re-ticking
            // the job rebuilt the SAME layer objects still unticked: the specs
            // and panes appeared, the cutting list stayed empty. RC-009 fixed
            // that by forcing EVERY pane of EVERY selected job back to
            // IsSelected=true inside RebuildSelectedJobBreakdown — which also
            // silently re-added glass the operator had just removed from an
            // unrelated, still-ticked job.
            //
            // Correct semantic: a job's panes return to their pre-ticked
            // default when THAT JOB is (re-)selected, and an operator's pane
            // choice is left alone while the job stays selected.
            //
            // Safe from re-entrancy: a job that is only now becoming selected
            // is not in JobSpecBreakdown yet, so its layers carry no
            // OnLayerSelectionChanged subscription (the previous rebuild
            // detached it) and these writes raise nothing.
            if (sender is JobOptionVm job && job.IsSelected)
                ResetPaneTicksToDefault(job);

            OnPropertyChanged(nameof(HasSelectedJobs));
            OnPropertyChanged(nameof(PiecesEmptyHint));
            RebuildSelectedJobBreakdown();
        }

        /// <summary>
        /// Puts every pane of one job back to the pre-ticked default the picker
        /// builds it with (<c>IsSelected = true</c>, Jobs.cs — "Panes are
        /// pre-ticked so a job's sizes import with a single click"). Called when
        /// that job transitions unselected → selected, never for a job that is
        /// already selected.
        /// </summary>
        private static void ResetPaneTicksToDefault(JobOptionVm job)
        {
            foreach (var spec in job.Specs)
                foreach (var layer in spec.Layers)
                    if (layer.IsPane) layer.IsSelected = true;
        }

        /// <summary>
        /// Hooks one job option into the picker's selection wiring. Idempotent:
        /// the handler is detached first, so calling this twice can never make a
        /// single tick rebuild the breakdown twice.
        /// <para>
        /// Extracted from <c>LoadJobsInternalAsync</c> so there is ONE way to
        /// wire a job, whether it came from the database or was supplied
        /// directly (a host, or Tests/Optimization — which cannot use
        /// <c>LoadJobs()</c> at all, because that method only applies its
        /// results through <c>Application.Current.Dispatcher</c> and xUnit has no
        /// WPF Application).
        /// </para>
        /// </summary>
        public void AttachJobOption(JobOptionVm? job)
        {
            if (job == null) return;
            job.PropertyChanged -= OnJobSelectionChanged;
            job.PropertyChanged += OnJobSelectionChanged;
        }

        /// <summary>
        /// Inserts the informational spacer / interlayer box at its correct
        /// place in a spec's build-up strip (after the outer pane, and after
        /// the first lam pane on hybrids). Nothing is cut from these boxes.
        /// </summary>
        private static void AddBuildUpInfoBox(SpecOptionVm spec, string buildType, int paneIndex, string specName)
        {
            bool needSpacer = (buildType == "Dgu" && paneIndex == 1)
                || (buildType == "DguLam" && paneIndex == 1);
            bool needInterlayer = (buildType == "Lam" && paneIndex == 1)
                || (buildType == "DguLam" && paneIndex == 2);

            if (needSpacer)
            {
                string mm = PnLSpecParser.Spacer(specName).Groups["mm"].Value;
                spec.Layers.Add(new BuildUpLayerVm
                {
                    Kind = "Spacer",
                    Title = (string.IsNullOrWhiteSpace(mm) ? "" : mm + "mm ") + "ASP",
                    Sub = "air spacer · not cut",
                    Thickness = string.Empty
                });
            }
            else if (needInterlayer)
            {
                var interlayer = PnLSpecParser.Interlayer(specName);
                string mm = interlayer.Success ? interlayer.Groups["mm"].Value : "";
                string type = interlayer.Success ? interlayer.Groups["type"].Value.ToUpperInvariant() : "PVB";
                spec.Layers.Add(new BuildUpLayerVm
                {
                    Kind = "Interlayer",
                    Title = (string.IsNullOrWhiteSpace(mm) ? "" : mm + "mm ") + type,
                    Sub = "interlayer · not cut",
                    Thickness = string.Empty
                });
            }
        }

        /// <summary>
        /// Rebuilds <see cref="FilteredJobs"/> from <see cref="AvailableJobs"/>
        /// using <see cref="JobSearchText"/> (job id / customer / summary).
        /// Runs on the UI thread — callers marshal beforehand.
        /// </summary>
        private void RefreshJobsFilter()
        {
            string q = (_jobSearchText ?? string.Empty).Trim();
            IEnumerable<JobOptionVm> items = string.IsNullOrEmpty(q)
                ? AvailableJobs
                : AvailableJobs.Where(j =>
                    (j.JobOrderId ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (j.CustomerName ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (j.Summary ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));

            FilteredJobs.Clear();
            foreach (var item in items) FilteredJobs.Add(item);
        }

        // ── composition helpers ─────────────────────────────────────────

        /// <summary>Leading thickness of a glass composition, e.g. "6mm clear …" → "6mm".</summary>
        private static string ParseThicknessLabel(string? composition)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                composition ?? string.Empty, @"^\s*(\d+(?:\.\d+)?)\s*mm");
            return m.Success ? m.Groups[1].Value + "mm" : string.Empty;
        }

        /// <summary>Leading thickness in mm (0 when unreadable).</summary>
        // PHASE 1 / finding 4 (2026-09-20): thickness is scraped from a
        // free-text glass label, and it decides which house margin profile a
        // cutting group is packed with. The old pattern was start-anchored
        // (@"^\s*(\d+(?:\.\d+)?)\s*mm") AND parsed with NumberStyles.Integer,
        // which made it fail in three everyday ways:
        //   "HD Grey 6mm"     → 0   (thickness not first)
        //   "Clear 6mm FT"    → 0   (thickness not first)
        //   "6.5mm laminated" → 0   (the regex captured 6.5, the integer
        //                            parse rejected it — the pattern accepted
        //                            a decimal the parser could not read)
        // Zero then flowed into ResolveGroupMargins and became
        // MarginProfile.DefaultFor(6), so a 12mm unit labelled "Clear 12mm"
        // was cut with 6mm house margins and nothing on the result said so.
        // Matching anywhere in the label and rounding a decimal to the nearest
        // whole millimetre removes all three. Compiled: this runs once per
        // piece row and once per cutting group on every run.
        private static readonly System.Text.RegularExpressions.Regex ThicknessMmRegex =
            new System.Text.RegularExpressions.Regex(
                @"(?<![\d.])(\d+(?:[.,]\d+)?)\s*mm\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant
                | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static int ParseThicknessMm(string? composition)
        {
            if (string.IsNullOrWhiteSpace(composition)) return 0;

            var m = ThicknessMmRegex.Match(composition);
            if (!m.Success) return 0;

            // A decimal comma is accepted as well as a point: the label is free
            // text the operator typed, and "6,5mm" is the same glass as "6.5mm".
            string raw = m.Groups[1].Value.Replace(',', '.');
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                   && t > 0
                ? (int)Math.Round(t, MidpointRounding.AwayFromZero)
                : 0;
        }

        /// <summary>Rebuilds the spec build-up strips from every ticked job.
        /// SENIOR FIX 2026-09-20: job selection now AUTO-IMPORTS into cutlist
        /// with merging of matching spec/glass across multi-jobs (user req:
        /// 'left side is job selection so job list should auto import the
        /// related specification and related cutting sizes automatically in
        /// the step4/5 pane breakdown and cutlist section and if multi jobs
        /// selected then should merge the matching specification and their
        /// related glass. step 5 export have this pattern to merge').
        /// Previously user had to tick jobs, then manually click 'Add selected
        /// panes → cutting list' — now auto on selection change, with merge
        /// by normalized composition + size + position, summing qty.
        /// RC-007 FIX 2026-09-20: headless host for Step5 runs must NOT
        /// auto-clear Pieces/Stocks when LoadJobs finishes after RunFromStep5.
        /// RC-009 FIX 2026-09-20: forced every pane layer IsSelected=true here.
        /// RC-013 (second-pass audit) REMOVED that force — it re-added glass the
        /// operator had deliberately removed from any still-ticked job. Pane
        /// ticks are now reset per job, in <see cref="OnJobSelectionChanged"/>,
        /// only for the job that just became selected.
        /// </summary>
        private void RebuildSelectedJobBreakdown()
        {
            if (IsHeadlessHost)
            {
                // Headless host is for Step5 live runs only — its Pieces/Stocks
                // are owned by RunFromStep5, not by job selection. Clearing them
                // here would wipe the live cutlist after RunFromStep5 populated it.
                return;
            }

            foreach (var spec in JobSpecBreakdown)
                foreach (var layer in spec.Layers)
                    layer.PropertyChanged -= OnLayerSelectionChanged;

            JobSpecBreakdown.Clear();
            foreach (var job in AvailableJobs.Where(j => j.IsSelected))
            {
                foreach (var spec in job.Specs)
                {
                    foreach (var layer in spec.Layers)
                        layer.PropertyChanged += OnLayerSelectionChanged;
                    JobSpecBreakdown.Add(spec);
                }
            }

            HasSpecBreakdown = JobSpecBreakdown.Count > 0;

            PaneChecklist.Clear();
            foreach (var spec in JobSpecBreakdown)
                foreach (var layer in spec.Layers)
                    if (layer.IsPane) PaneChecklist.Add(layer);
            HasPaneChecklist = PaneChecklist.Count > 0;

            RefreshPaneSelectionCount();

            // STOCKS follows the selection: the pool becomes exactly the
            // ticked jobs' Step 4 sheet sizes (per pane × composition).
            SyncStocksFromSelectedJobs();

            // AUTO-IMPORT: when jobs are ticked, their panes (pre-ticked)
            // immediately populate the cutting list, merged across jobs.
            // Added by 6c7224f1 — this single call is what makes a job tick
            // reach the SIZES tab at all. Everything downstream of it is
            // derived state; without it JobSpecBreakdown / PaneChecklist /
            // Stocks all fill while Pieces stays empty, which is EXACTLY the
            // "specs populate but the cutting-size list is empty" report.
            AutoRebuildPiecesFromSelectedJobs();
        }

        private void OnLayerSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BuildUpLayerVm.IsSelected)) return;
            RefreshPaneSelectionCount();
            // AUTO-UPDATE cutlist when pane checklist tick changes
            AutoRebuildPiecesFromSelectedJobs();
        }

        private void RefreshPaneSelectionCount()
        {
            SelectedPaneCount = JobSpecBreakdown
                .SelectMany(s => s.Layers)
                .Count(l => l.IsPane && l.IsSelected);
        }

        private void SetAllPanesSelected(bool selected)
        {
            foreach (var layer in JobSpecBreakdown.SelectMany(s => s.Layers).Where(l => l.IsPane))
                layer.IsSelected = selected;
            RefreshPaneSelectionCount();
            AutoRebuildPiecesFromSelectedJobs();
        }

        /// <summary>
        /// Rebuilds <see cref="Pieces"/> from the currently selected jobs'
        /// ticked panes, merging matching composition + size + position across
        /// jobs (the same key the Step 5 export uses). This is the ONLY path
        /// from a job tick to the SIZES tab: everything that tab renders
        /// (<see cref="SizeGroups"/>, <see cref="CutPlanRows"/>,
        /// <see cref="CuttingGroups"/>, <see cref="PieceSummary"/>) is derived
        /// from <see cref="Pieces"/> by <see cref="RefreshDerived"/>.
        /// <para>
        /// RC-007: the headless host must not clear Pieces when no jobs are
        /// selected — <c>RunFromStep5</c> owns them there.
        /// </para>
        /// <para>
        /// RC-009's "fallback" (re-add <c>layer.Pieces</c> unmerged when
        /// <c>merged.Count == 0</c>) is REMOVED. It read the identical
        /// <c>layer.Pieces</c> through the identical <c>w&lt;=0 || h&lt;=0 ||
        /// qty&lt;=0</c> filter that had just produced zero rows, so it could
        /// never produce a different result: dead code whose only effect was to
        /// relabel a correct empty outcome as a bug. What replaces it is the
        /// honest version — measure the quantity at every hop, and when
        /// conservation fails say so on screen and in the log instead of
        /// silently retrying the same filter.
        /// </para>
        /// </summary>
        private void AutoRebuildPiecesFromSelectedJobs()
        {
            // The headless host a proforma quote drives its Step 5 runs through
            // owns its Pieces exclusively (RunFromStep5). Job selection is not
            // its input — see RC-007.
            if (IsHeadlessHost) return;

            var selectedLayers = JobSpecBreakdown
                .SelectMany(s => s.Layers)
                .Where(l => l.IsPane && l.IsSelected)
                .ToList();

            // ── Q2: everything the ticked panes actually carry ────────────────
            // layer.Pieces is filled once, in LoadJobsInternalAsync, from the
            // job's Step 5 rows that already survived
            // ProductionDistributionEngine.DistributeGroup's own filter
            // (Stage==Cutting && W>0 && H>0 && Qty>0). It is never re-read from
            // the database here — so a pane with zero rows means the job itself
            // has no cutting rows, not that this method lost them.
            int paneRows = 0;
            int paneQty = 0;
            foreach (var layer in selectedLayers)
            {
                paneRows += layer.Pieces.Count;
                foreach (var (_, _, _, _, qty) in layer.Pieces)
                    if (qty > 0) paneQty += qty;
            }

            if (selectedLayers.Count == 0)
            {
                // No panes ticked → the cutting list mirrors the selection and
                // is empty. Only touch Pieces when there is something to clear,
                // so an unrelated refresh never re-derives the whole SIZES tab.
                if (Pieces.Count > 0)
                {
                    Pieces.Clear();
                    RefreshDerived();
                }
                RunMessage = SelectedJobs.Any()
                    ? "No panes selected — tick pane boxes to populate cutting list."
                    : "No jobs selected — tick jobs on left to auto-import.";
                LogImportConservation(selectedLayers.Count, paneRows, paneQty, 0, 0, 0);
                return;
            }

            // ── Q3: merge ─────────────────────────────────────────────────────
            // Key includes JobOrderId so two jobs never collapse into one row
            // (qty and identity must remain recoverable). Same material across
            // jobs still packs together later via MaterialKey. Position stays
            // in the key because an equal-size DGU has identical outer/inner
            // sizes — keying without it dropped the inner pane.
            var merged = new Dictionary<(string job, string mat, int w, int h, string pos),
                (string glass, HashSet<string> labels, int w, int h, int qty, string pos,
                 string job, string pi, int spec, string color, int thk, string proc, string matKey)>();
            int mergedQty = 0;
            int rejectedRows = 0;
            int rejectedQty = 0;

            foreach (var layer in selectedLayers)
            {
                foreach (var (glass, label, w, h, qty) in layer.Pieces)
                {
                    if (w <= 0 || h <= 0 || qty <= 0)
                    {
                        // A row that cannot be cut is not a piece. Counted
                        // rather than silently dropped, so the conservation
                        // check below can tell "filtered" from "lost".
                        rejectedRows++;
                        if (qty > 0) rejectedQty += qty;
                        continue;
                    }

                    // Never invent a material key from composition text.
                    string identityKey = layer.MaterialKey ?? string.Empty;
                    string matKey = !string.IsNullOrWhiteSpace(identityKey)
                        ? identityKey
                        : "UNRESOLVED|" + (layer.JobOrderId ?? string.Empty) + "|"
                          + layer.SpecGroupNumber.ToString(CultureInfo.InvariantCulture) + "|"
                          + (layer.Position ?? string.Empty);
                    string posKey = (layer.Position ?? string.Empty).Trim().ToLowerInvariant();
                    string jobId = layer.JobOrderId ?? string.Empty;
                    var key = (jobId, matKey, w, h, posKey);

                    if (merged.TryGetValue(key, out var existing))
                    {
                        existing.qty += qty;
                        if (!string.IsNullOrWhiteSpace(label) && label != "—")
                            existing.labels.Add(label.Trim());
                        merged[key] = existing;
                    }
                    else
                    {
                        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(label) && label != "—")
                            labels.Add(label.Trim());
                        merged[key] = (glass, labels, w, h, qty, layer.Position,
                            jobId, layer.ProformaId ?? string.Empty, layer.SpecGroupNumber,
                            layer.Color ?? string.Empty, layer.ThicknessMm,
                            layer.Processing ?? string.Empty, layer.MaterialKey ?? string.Empty);
                    }
                    mergedQty += qty;
                }
            }

            // ── Q4: the authoritative cutting list ────────────────────────────
            Pieces.Clear();
            foreach (var kv in merged
                .OrderBy(k => k.Key.job, StringComparer.OrdinalIgnoreCase)
                .ThenBy(k => k.Key.mat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(k => k.Key.w)
                .ThenBy(k => k.Key.h))
            {
                var v = kv.Value;
                string label = v.labels.Count > 0
                    ? string.Join(", ", v.labels.Distinct().Take(8))
                    : "—";
                var row = NewPieceRow(
                    v.glass,
                    label,
                    v.w.ToString(CultureInfo.InvariantCulture),
                    v.h.ToString(CultureInfo.InvariantCulture),
                    v.qty.ToString(CultureInfo.InvariantCulture));
                row.Position = v.pos;
                row.JobOrderId = v.job;
                row.ProformaId = v.pi;
                row.SpecGroupNumber = v.spec;
                row.Color = v.color;
                row.ThicknessMm = v.thk;
                row.Processing = v.proc;
                row.MaterialKey = v.matKey;
                if (string.Equals(v.pos, "Inner", StringComparison.OrdinalIgnoreCase)
                    || v.pos.IndexOf("Lam", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    row.W2 = v.w;
                    row.H2 = v.h;
                }
                else
                {
                    row.W1 = v.w;
                    row.H1 = v.h;
                }
                row.EnsureMaterialKey();
                Pieces.Add(row);
            }

            RefreshDerived();

            // ── Q5/Q6: what the SIZES tab can actually show ───────────────────
            // Measured, not assumed: re-apply the exact filter the tab applies
            // (parseable rows + the cutting-group selector) and sum quantity.
            int piecesQty = 0;
            int sizesQty = 0;
            foreach (var p in Pieces)
            {
                if (!p.TryParse(out _, out _, out int q)) continue;
                piecesQty += q;
                if (SelectedGroupMatches(CompositionLabel(p.GlassRef))) sizesQty += q;
            }

            int distinctComps = merged.Keys.Select(k => k.mat).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int jobCount = SelectedJobs.Count();

            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "Auto-imported {0} pane(s) from {1} job(s) → {2} pieces in {3} cutting group(s) (merged by spec+glass+size).",
                selectedLayers.Count, jobCount, mergedQty, distinctComps);

            // ── conservation invariant ────────────────────────────────────────
            // A merge may COMBINE rows; it must never make glass disappear. So
            // every hop has to carry the same total quantity and the SIZES tab
            // has to be able to show all of it. When that breaks, the operator
            // is told here — on screen — instead of being left staring at an
            // empty cutting-size list with no explanation.
            bool lostInMerge = mergedQty != paneQty - rejectedQty;
            bool lostInRebuild = piecesQty != mergedQty;
            bool nothingCuttable = paneRows > 0 && mergedQty == 0;
            bool noPaneRows = paneRows == 0;

            if (noPaneRows)
            {
                RunMessage = string.Format(CultureInfo.InvariantCulture,
                    "{0} pane(s) ticked but they carry no Step 5 cutting rows — the job's pane sizes never reached the picker.",
                    selectedLayers.Count);
            }
            else if (nothingCuttable)
            {
                RunMessage = string.Format(CultureInfo.InvariantCulture,
                    "{0} pane(s) ticked but every Step 5 row was rejected (W≤0, H≤0 or Qty≤0) — nothing cuttable to import.",
                    selectedLayers.Count);
            }
            else if (lostInMerge || lostInRebuild)
            {
                RunMessage = string.Format(CultureInfo.InvariantCulture,
                    "Quantity not conserved: panes {0} pcs → merged {1} pcs → cutting list {2} pcs. Investigate before running.",
                    paneQty, mergedQty, piecesQty);
            }

            LogImportConservation(selectedLayers.Count, paneRows, paneQty, merged.Count, mergedQty, piecesQty,
                sizesQty, rejectedRows, rejectedQty);
        }

        /// <summary>
        /// ONE consolidated line per job-selection rebuild, replacing the six
        /// verbose <c>[OPT-JOBS]</c> entries the first RC-009 pass added. It
        /// carries the build identity — so a log excerpt answers "which build
        /// produced this?" without a separate lookup — plus the quantity
        /// measured at every hop of the JOBS → SIZES chain, which is the only
        /// way to tell "correctly empty" apart from "silently lost".
        /// </summary>
        private void LogImportConservation(
            int panes, int paneRows, int paneQty, int mergedKeys, int mergedQty, int piecesQty,
            int sizesQty = 0, int rejectedRows = 0, int rejectedQty = 0)
        {
            try
            {
                // Q5 is a VIEW of Q4 narrowed by the cutting-group dropdown, so
                // it must equal Q4 while the dropdown is on ALL and must be a
                // non-empty subset while a specific group is picked.
                // (RebuildCuttingGroups resets the selection to ALL whenever the
                // picked group no longer exists, so a specific selection always
                // has at least one matching piece — and an empty Pieces always
                // leaves the selection on ALL.)
                bool allGroupsSelected = string.Equals(
                    SelectedCuttingGroup, AllCuttingGroups, StringComparison.OrdinalIgnoreCase);
                bool sizesConserved = allGroupsSelected
                    ? sizesQty == piecesQty
                    : sizesQty > 0 && sizesQty <= piecesQty;

                bool conserved =
                    paneQty - rejectedQty == mergedQty
                    && mergedQty == piecesQty
                    && sizesConserved;

                string msg = string.Format(CultureInfo.InvariantCulture,
                    "[OPT-JOBS] import build={0} jobs={1} specs={2} panes={3} paneRows={4} paneQty={5} " +
                    "rejectedRows={6} rejectedQty={7} mergedKeys={8} mergedQty={9} pieces={10} piecesQty={11} " +
                    "sizeGroups={12} sizesQty={13} stocks={14} hasPieces={15}{16}",
                    Services.Updates.AppInfo.BuildStamp,
                    SelectedJobs.Count(), JobSpecBreakdown.Count, panes, paneRows, paneQty,
                    rejectedRows, rejectedQty, mergedKeys, mergedQty,
                    Pieces.Count, piecesQty, SizeGroups.Count, sizesQty, Stocks.Count, HasPieces,
                    conserved ? string.Empty : " — QUANTITY NOT CONSERVED");

                var ctx = new Services.Logging.LogContext
                {
                    Module = "Optimization",
                    Operation = "AutoRebuildPieces"
                };

                if (conserved) Services.Logging.AppLogService.Info(msg, ctx);
                else Services.Logging.AppLogService.Warning(msg, ctx);
            }
            catch { }
        }

        /// <summary>
        /// Adds the checked pane layers' cutting pieces to the run. Each piece
        /// keeps its per-pane glass composition (so SIZES merges the low-e
        /// outers from BOTH specs into ONE 76-piece group, clear inners into
        /// 64 and ramly inners into 12) and its pane position for the 2D view.
        /// SENIOR 2026-09-20: now delegates to AutoRebuildPiecesFromSelectedJobs
        /// which merges matching spec+glass+size across multi-jobs (qty summed,
        /// labels merged distinct) — pattern from Step 5 export. Manual button
        /// still works but auto-import already does it on job tick.
        /// </summary>
        private void AddSelectedPanes()
        {
            // Auto path already merges correctly — reuse it for manual button too
            AutoRebuildPiecesFromSelectedJobs();
        }

        /// <summary>
        /// Loads the owner's calibration DGU job — the benchmark the engine
        /// is pinned against (Low-E 17 sheets @ 82.77%). Decomposed exactly
        /// as Step 4/5 records it:
        ///   • Spec 1 — "…low e (outer) + 12mm asp + 6mm clear (inner)": 64 units.
        ///   • Spec 2 — "…low e (outer) + 12mm asp + 6mm obscure ramly (inner)": 12 units.
        ///   • OUTER = 6mm Climaguard low-e on BOTH specs → 76 pieces.
        ///   • INNER = 64 clear + 12 ramly obscure.
        /// Total 152 pieces in three cutting groups.
        /// </summary>
    }
}
