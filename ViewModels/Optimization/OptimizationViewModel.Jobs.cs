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

            public List<(string Glass, string Label, int W, int H, int Qty)> Pieces { get; } = new();

            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
            }
        }

        public ObservableCollection<JobOptionVm> AvailableJobs { get; } = new();

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
        public string PiecesEmptyHint => HasPieces
            ? string.Empty
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
                    var plan = ProductionDistributionEngine.Distribute(jo.JobOrderId, invoice);

                    var cut = plan.Items
                        .Where(i => i.Stage == ProductionStage.Cutting && i.WidthMm > 0 && i.HeightMm > 0)
                        .ToList();

                    var opt = new JobOptionVm
                    {
                        JobOrderId = jo.JobOrderId,
                        SourceProformaId = jo.SourceProformaId ?? string.Empty,
                        CustomerName = jo.CustomerName
                    };

                    // Surface the distribution engine's import warnings
                    // (previously discarded): a row with no quantity becomes
                    // 1 piece, a row without W2/H2 loses its inner pane, an
                    // unparseable spec name loses treatment routing — each of
                    // these changes the cutting list silently.
                    opt.Warnings.AddRange(plan.Warnings);

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
                                IsSelected = true
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

                    if (opt.Specs.Count > 0) options.Add(opt);
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
                            o.PropertyChanged += OnJobSelectionChanged;
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
                        // Phase 3: say how many of how many — a silent cap
                        // reads as missing jobs.
                        string truncationNote = totalEligible > jobListDisplayCap
                            ? string.Format(CultureInfo.InvariantCulture,
                                " Showing the {0} most recent of {1}.",
                                jobListDisplayCap, totalEligible)
                            : string.Empty;
                        JobsStatusText = options.Count == 0
                            ? "No confirmed job orders with cutting pieces yet."
                            : string.Format(CultureInfo.InvariantCulture,
                                "{0} confirmed job order(s) ready.{1}{2}",
                                options.Count,
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
            RebuildSelectedJobBreakdown();
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
        private static int ParseThicknessMm(string? composition)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                composition ?? string.Empty, @"^\s*(\d+(?:\.\d+)?)\s*mm");
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int t) ? t : 0;
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
        /// SENIOR: auto rebuilds Pieces from currently selected jobs/panes,
        /// merging matching spec + glass + size + position across multi-jobs.
        /// Pattern mirrors Step 5 export merging: same normalized composition
        /// + same W×H + same position → qty summed, labels merged distinct.
        /// This is what user requested for multi-job selection.
        /// RC-007: headless host must not clear Pieces when no jobs selected.
        /// </summary>
        private void AutoRebuildPiecesFromSelectedJobs()
        {
            if (IsHeadlessHost) return;

            var selectedLayers = JobSpecBreakdown
                .SelectMany(s => s.Layers)
                .Where(l => l.IsPane && l.IsSelected)
                .ToList();

            if (selectedLayers.Count == 0)
            {
                // No panes ticked → clear cutlist (job deselected)
                if (Pieces.Count > 0)
                {
                    Pieces.Clear();
                    RefreshDerived();
                    RunMessage = SelectedJobs.Any()
                        ? "No panes selected — tick pane boxes to populate cutting list."
                        : "No jobs selected — tick jobs on left to auto-import.";
                }
                return;
            }

            // Merge key: normalized composition + W + H + position (case-insensitive)
            // Value: original glass string, distinct labels, qty sum, position
            var merged = new Dictionary<(string comp, int w, int h, string pos), (string glass, HashSet<string> labels, int w, int h, int qty, string pos)>();

            foreach (var layer in selectedLayers)
            {
                foreach (var (glass, label, w, h, qty) in layer.Pieces)
                {
                    if (w <= 0 || h <= 0 || qty <= 0) continue;
                    string normComp = CompositionKey.Normalize(glass);
                    string posKey = (layer.Position ?? string.Empty).Trim().ToLowerInvariant();
                    var key = (normComp, w, h, posKey);

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
                        merged[key] = (glass, labels, w, h, qty, layer.Position);
                    }
                }
            }

            // Rebuild Pieces from merged dict, ordered by composition then size
            Pieces.Clear();
            foreach (var kv in merged
                .OrderBy(k => k.Key.comp, StringComparer.OrdinalIgnoreCase)
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
                Pieces.Add(row);
            }

            RefreshDerived();

            int totalPcs = merged.Values.Sum(x => x.qty);
            int distinctComps = merged.Keys.Select(k => k.comp).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int jobCount = SelectedJobs.Count();
            RunMessage = string.Format(CultureInfo.InvariantCulture,
                "Auto-imported {0} pane(s) from {1} job(s) → {2} pieces in {3} cutting group(s) (merged by spec+glass+size).",
                selectedLayers.Count, jobCount, totalPcs, distinctComps);
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
