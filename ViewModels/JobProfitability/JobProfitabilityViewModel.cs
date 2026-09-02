// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/JobProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Profitability (PHASE C)
//
// WHAT IT IS
//   A portfolio view of the Step 9 Profit & Loss data. Step 9 answers
//   "is THIS invoice profitable?"; this module answers "which confirmed
//   jobs, customers and salesmen are making us money?".
//
// DATA SOURCE
//   ONLY proforma invoices whose stored Status is "Approved" are read —
//   that is the exact value the Saved PI list persists when the operator
//   marks a PI as "PI Confirmed". Draft ("PI created"), Sent ("PI sent"),
//   Rejected, Cancelled and Expired rows are never included, so the
//   report shows only the business the customer actually confirmed.
//
//   Every figure on a row comes straight from the persisted Step 9 P&L:
//   NetExVat, Profit and MarginPercent are Step 9's own numbers; cost is
//   Step 9's TotalGlassCost + TotalProductionCost. No margin is recomputed
//   from the invoice header, and no VAT divisor lives here.
//
// FILTERS
//   CompanyFilter / SalesmanFilter / CustomerFilter — "All" plus every
//   distinct value found among CONFIRMED PIs
//   SearchText — matches proforma ID, customer or project (case-insensitive)
//   Totals and the loss count reflect the FILTERED set, so drilling into
//   one salesman immediately re-totals for that salesman. Uncosted rows
//   stay visible (greyed) but are EXCLUDED from totals: including their
//   revenue with zero profit is what used to dilute the overall margin.
//
// SAFETY: LoadAsync never throws. A DB failure leaves the list empty and
// says so in StatusMessage.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.JobProfitability
{
    public class JobProfitabilityViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices = null!;

        public JobProfitabilityViewModel(IProformaInvoiceRepository? invoices)
        {
            _invoices = invoices!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        public string Title { get; } = "JOB PROFITABILITY";

        public string Subtitle { get; } =
            "Profit, cost and margin for PI-confirmed proformas only — " +
            "figures are read straight from the Step 9 Profit & Loss snapshot.";

        /// <summary>Sentinel used by both filter dropdowns.</summary>
        public const string AllFilter = "All";

        // Full unfiltered set; Rows is the filtered projection of this.
        private readonly List<JobProfitabilityRow> _allRows = new();

        /// <summary>Filtered rows shown in the list.</summary>
        public BulkObservableCollection<JobProfitabilityRow> Rows { get; } = new();

        /// <summary>Distinct companies, "All" first.</summary>
        public ObservableCollection<string> CompanyFilterOptions { get; } = new();

        /// <summary>Distinct salesmen, "All" first.</summary>
        public ObservableCollection<string> SalesmanFilterOptions { get; } = new();

        /// <summary>Distinct customers, "All" first.</summary>
        public ObservableCollection<string> CustomerFilterOptions { get; } = new();

        // ── Filters ───────────────────────────────────────────────────────

        private string _companyFilter = AllFilter;
        public string CompanyFilter
        {
            get => _companyFilter;
            set
            {
                if (_companyFilter == value) return;
                _companyFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _salesmanFilter = AllFilter;
        public string SalesmanFilter
        {
            get => _salesmanFilter;
            set
            {
                if (_salesmanFilter == value) return;
                _salesmanFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _customerFilter = AllFilter;
        public string CustomerFilter
        {
            get => _customerFilter;
            set
            {
                if (_customerFilter == value) return;
                _customerFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _dateRangeFilter = "All";
        public string DateRangeFilter
        {
            get => _dateRangeFilter;
            set
            {
                if (_dateRangeFilter == value) return;
                _dateRangeFilter = value ?? "All";
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        /// <summary>Date range options.</summary>
        public ObservableCollection<string> DateRangeOptions { get; } = new()
        {
            "All", "This Week", "This Month", "This Quarter", "This Year", "Last 30 Days", "Last 90 Days"
        };

        private string _searchText = "";
        /// <summary>Free text over proforma ID, customer and project.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? "";
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ── Totals (always reflect the FILTERED set) ──────────────────────

        private decimal _totalNet;
        public decimal TotalNet
        {
            get => _totalNet;
            private set
            {
                if (_totalNet == value) return;
                _totalNet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalNetDisplay));
            }
        }

        private decimal _totalCost;
        public decimal TotalCost
        {
            get => _totalCost;
            private set
            {
                if (_totalCost == value) return;
                _totalCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCostDisplay));
            }
        }

        private decimal _totalProfit;
        public decimal TotalProfit
        {
            get => _totalProfit;
            private set
            {
                if (_totalProfit == value) return;
                _totalProfit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalProfitDisplay));
                OnPropertyChanged(nameof(TotalProfitLabel));
            }
        }

        private int _lossCount;
        /// <summary>How many filtered jobs lost money.</summary>
        public int LossCount
        {
            get => _lossCount;
            private set
            {
                if (_lossCount == value) return;
                _lossCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LossCountDisplay));
                OnPropertyChanged(nameof(HasLosses));
            }
        }

        private decimal _overallMarginPercent;
        public decimal OverallMarginPercent
        {
            get => _overallMarginPercent;
            private set
            {
                if (_overallMarginPercent == value) return;
                _overallMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallMarginDisplay));
            }
        }

        public string OverallMarginDisplay => $"{OverallMarginPercent:0.#}%";

        private decimal _averageMarginPercent;
        /// <summary>
        /// Simple average of the Margin % shown on the displayed, costed rows:
        /// Σ (row MarginPercent) ÷ number of valid rows. No amount-based
        /// math — this deliberately weights every job equally, which is a
        /// different question from OverallMarginPercent (portfolio
        /// TotalProfit ÷ TotalNet, which Large jobs dominate).
        /// </summary>
        public decimal AverageMarginPercent
        {
            get => _averageMarginPercent;
            private set
            {
                if (_averageMarginPercent == value) return;
                _averageMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageMarginDisplay));
                OnPropertyChanged(nameof(IsAverageLoss));
            }
        }

        private int _averageMarginCount;
        /// <summary>How many rows contributed to the average (0 = no valid rows).</summary>
        public int AverageMarginCount
        {
            get => _averageMarginCount;
            private set
            {
                if (_averageMarginCount == value) return;
                _averageMarginCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageMarginDisplay));
                OnPropertyChanged(nameof(IsAverageLoss));
            }
        }

        /// <summary>
        /// "12.3%" when at least one valid row exists; "—" when every
        /// displayed row is uncosted or the list is empty.
        /// </summary>
        public string AverageMarginDisplay => AverageMarginCount > 0
            ? $"{AverageMarginPercent:0.#}%"
            : "—";

        /// <summary>
        /// True when the AVERAGE itself is negative. Deliberately NOT the
        /// portfolio's IsPortfolioLoss: a net-loss portfolio can still have
        /// a positive average margin (and vice versa), so the colour on this
        /// tile must mirror the number it displays.
        /// </summary>
        public bool IsAverageLoss => AverageMarginCount > 0
            && AverageMarginPercent < 0m;

        /// <summary>True when the portfolio total is negative.</summary>
        public bool IsPortfolioLoss => TotalProfit < 0m;

        /// <summary>True when at least one filtered job is loss-making.</summary>
        public bool HasLosses => LossCount > 0;

        public string TotalNetDisplay => $"AED {TotalNet:N2}";
        public string TotalCostDisplay => $"AED {TotalCost:N2}";
        public string TotalProfitDisplay => $"AED {Math.Abs(TotalProfit):N2}";
        public string TotalProfitLabel => IsPortfolioLoss ? "TOTAL LOSS" : "TOTAL PROFIT";
        public string LossCountDisplay => $"{LossCount}";

        // ── State ─────────────────────────────────────────────────────────

        /// <summary>
        /// Set when a refresh arrives while a load is already running, so
        /// the request is re-run instead of dropped. See LoadAsync.
        /// </summary>
        private bool _reloadRequested;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True when the filtered list has rows (drives the empty state).</summary>
        public bool HasRows => Rows.Count > 0;

        private string _statusMessage = "Loading job profitability…";
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value ?? "";
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        /// <summary>Reloads everything from the invoice database.</summary>
        public ICommand RefreshCommand { get; private set; }

        public JobProfitabilityViewModel()
         : this(
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh"));

            Rows.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRows));

            CompanyFilterOptions.Add(AllFilter);
            SalesmanFilterOptions.Add(AllFilter);
            CustomerFilterOptions.Add(AllFilter);

            // ══════════════════════════════════════════════════════════
            //  LIVE UPDATES
            //
            //  Entering the module reloaded it, but saving an invoice while
            //  this screen was already open left the figures stale with
            //  nothing on screen saying so. The repository now raises a
            //  signal on every insert, update and delete, so the list
            //  follows the data instead of the navigation.
            //
            //  The handler marshals to the UI thread: a save may complete on
            //  a background thread, and rebuilding an ObservableCollection
            //  bound to a DataGrid from off-thread throws.
            //
            //  The subscription is never removed. This view model lives for
            //  the whole application session (it is a shell singleton), so
            //  there is no leak to unwind — and unsubscribing on a screen
            //  change is exactly what would reintroduce the stale list.
            // ══════════════════════════════════════════════════════════
            // The namespace and the class share the name
            // "ProformaInvoiceRepository", so a partial qualification is
            // ambiguous. The using at the top of this file already imports
            // the namespace, so the bare class name is what resolves.
            ProformaInvoiceRepository.SavedInvoicesChanged += OnSavedInvoicesChanged;

            // First load is fire-and-forget so construction stays cheap and
            // the shell can build every module VM without blocking.
            AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh");
        }

        private void OnSavedInvoicesChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null || dispatcher.CheckAccess())
                AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh");
            else
                dispatcher.BeginInvoke(new Action(() => AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh")));
        }

        private async Task FireRefreshAsync()
        {
            await LoadAsync().ConfigureAwait(true);
        }

        // ══════════════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads ONLY proforma invoices whose stored Status is "Approved"
        /// (the value the Saved PI list writes when the operator marks a PI
        /// as "PI Confirmed" — Draft / "PI created", Sent / "PI sent",
        /// Rejected, Cancelled and Expired never qualify), projects each to
        /// a profitability row and rebuilds the filter option lists. Never
        /// throws.
        /// </summary>
        public async Task LoadAsync()
        {
            // ══════════════════════════════════════════════════════════
            //  WHY THIS IS NOT JUST "if (IsBusy) return;"
            //
            //  The constructor kicks off a fire-and-forget first load, and
            //  Navigate("JobProfitability") calls RefreshCommand on entry.
            //  Open the module while that first load is still running - or
            //  save an invoice and come straight back - and the refresh hit
            //  the IsBusy guard and was SILENTLY DROPPED. The user saw
            //  stale figures and no indication anything had been skipped,
            //  which is exactly the "not updating instantly" symptom.
            //
            //  Now a refresh that arrives mid-load is REMEMBERED and re-run
            //  once the in-flight load finishes, so the last request always
            //  wins and the view ends up consistent with the database.
            // ══════════════════════════════════════════════════════════
            if (IsBusy)
            {
                _reloadRequested = true;
                return;
            }

            IsBusy = true;

            try
            {
                var repo = _invoices;
                if (repo == null)
                {
                    StatusMessage = "Invoice repository is not available.";
                    return;
                }

                // The saved entity stores the lifecycle exactly as the Saved
                // PI list writes it: the "PI Confirmed" action persists the
                // literal status "Approved"; everything else — Draft (shown
                // as "PI created"), Sent (shown as "PI sent"), Rejected,
                // Cancelled, Expired — stays out. Read the indexed Status
                // column first so drafts / sent / cancelled rows never even
                // leave the database with their full snapshot blob, then
                // re-check case-insensitively so a hand-edited value can
                // never slip in.
                var invoices = await repo.GetByStatusAsync("Approved")
                    .ConfigureAwait(true);

                _allRows.Clear();

                foreach (var inv in invoices ?? new List<Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice>())
                {
                    if (inv == null) continue;

                    if (!string.Equals(
                            inv.Status, "Approved",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    // DeserializeSnapshot already guards malformed JSON and
                    // returns an empty snapshot rather than throwing.
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);

                    var pl = snap?.ProfitLoss;

                    decimal glass = pl?.TotalGlassCost ?? 0m;
                    decimal production = pl?.TotalProductionCost ?? 0m;
                    decimal cost = glass + production;

                    // An invoice confirmed before Step 9 existed has no rows
                    // and no totals — flag it instead of pretending it is
                    // pure profit.
                    // ══════════════════════════════════════════════════
                    //  WHY COST / PROFIT / MARGIN CAME BACK EMPTY
                    //
                    //  These figures are read ONLY from the persisted Step 9
                    //  snapshot. ProformaInvoiceMapper writes that snapshot
                    //  from whatever the Step 9 view-model happens to hold at
                    //  save time - and Step 9 only fills itself in when the
                    //  user actually opens the tab and it recalculates.
                    //
                    //  So an invoice quoted and saved without visiting Step 9
                    //  is stored with TotalGlassCost = TotalProductionCost =
                    //  Profit = 0. The grid then rendered "AED 0.00" and
                    //  "not costed" for every such job, which reads as an
                    //  empty column.
                    //
                    //  That is a REPORTING gap, not a data-entry mistake by
                    //  the user, so the honest fix is to say which it is
                    //  rather than silently show zeros. IsCosted already
                    //  drives the greyed-out styling; the status line now
                    //  reports how many jobs are affected so the emptiness
                    //  is explained instead of just observed.
                    // ══════════════════════════════════════════════════
                    // Detect figures produced before the inner-pane fix.
                    // Read-only: it reports, it never rewrites saved money.
                    var integrity = ProfitLossIntegrityCheck.ScanSnapshot(pl);

                    bool costed = pl != null
                                  && (pl.Rows.Count > 0
                                      || cost > 0m
                                      || pl.NetExVat > 0m);


                    // ══════════════════════════════════════════════════
                    //  JOB PROFITABILITY READS STEP 9 — IT DOES NOT
                    //  RECOMPUTE.
                    //
                    //  Step 9 is the single source of truth for money:
                    //  NetExVat, Profit and MarginPercent are persisted in
                    //  the snapshot as they were shown to the user, and
                    //  Step 9's own margin is Profit ÷ NetExVat (already
                    //  rounded to one decimal in the snapshot).
                    //
                    //  The previous pattern recomputed here — it divided the
                    //  invoice header gross by 1.05 for step 9's net and
                    //  derived profit from net − cost, then re-derived margin
                    //  from those two. Step 9 stores the same numbers, so
                    //  this module could quote a DIFFERENT margin than the
                    //  user saw when the costing was taken (the header is
                    //  re-editable after Step 9, snapshots can be stale, and
                    //  Step 9 rounds profit/net before persisting it). No
                    //  VAT divisor or margin formula lives here any more.
                    //
                    //  Uncosted (confirmed before Step 9 existed, no rows, no
                    //  totals) → net/profit/margin 0 with the greyed "not
                    //  costed" treatment and EXCLUSION from totals, instead
                    //  of a header-derived guess.
                    // ══════════════════════════════════════════════════
                    decimal net = costed ? pl!.NetExVat : 0m;
                    decimal profit = costed ? pl!.Profit : 0m;
                    decimal margin = costed ? pl!.MarginPercent : 0m;

                    _allRows.Add(new JobProfitabilityRow
                    {
                        ProformaId = inv.ProformaId ?? "",
                        CompanyName = inv.CompanyName ?? "",
                        CustomerName = inv.CustomerName ?? "",
                        ProjectName = inv.ProjectName ?? "",
                        Salesman = snap?.Client?.SalesRepresentative ?? "",
                        IssueDate = inv.IssueDate,
                        NetExVat = net,
                        TotalCost = cost,
                        Profit = profit,
                        MarginPercent = margin,
                        IsLoss = costed && profit < 0m,
                        IsCosted = costed,
                        NeedsRecalculation = costed && integrity.NeedsRecalculation,
                        RecalculationWarning = integrity.Reason
                    });
                }

                RebuildFilterOptions();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[JobProfitabilityViewModel] Load failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Could not load job profitability — check the invoice database.";
            }
            finally
            {
                IsBusy = false;
            }

            // A refresh that arrived mid-load runs now. Cleared BEFORE the
            // re-entry so a load that keeps getting interrupted cannot spin
            // forever - at most one extra pass per dropped request.
            if (_reloadRequested)
            {
                _reloadRequested = false;
                await LoadAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Refreshes both dropdowns from the loaded data, preserving the
        /// current selection when it still exists.
        /// </summary>
        private void RebuildFilterOptions()
        {
            string company = CompanyFilter;
            string salesman = SalesmanFilter;

            CompanyFilterOptions.Clear();
            CompanyFilterOptions.Add(AllFilter);
            foreach (var c in _allRows
                         .Select(r => r.CompanyName)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                CompanyFilterOptions.Add(c);
            }

            SalesmanFilterOptions.Clear();
            SalesmanFilterOptions.Add(AllFilter);
            foreach (var s in _allRows
                         .Select(r => r.Salesman)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                SalesmanFilterOptions.Add(s);
            }

            // Customer filter options
            string customer = CustomerFilter;
            CustomerFilterOptions.Clear();
            CustomerFilterOptions.Add(AllFilter);
            foreach (var cu in _allRows
                         .Select(r => r.CustomerName)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                CustomerFilterOptions.Add(cu);
            }

            // Restore selections without re-triggering a filter pass.
            _companyFilter = CompanyFilterOptions.Contains(company) ? company : AllFilter;
            _salesmanFilter = SalesmanFilterOptions.Contains(salesman) ? salesman : AllFilter;
            _customerFilter = CustomerFilterOptions.Contains(customer) ? customer : AllFilter;
            OnPropertyChanged(nameof(CompanyFilter));
            OnPropertyChanged(nameof(SalesmanFilter));
            OnPropertyChanged(nameof(CustomerFilter));
        }

        // ══════════════════════════════════════════════════════════════
        //  FILTER + TOTALS
        // ══════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            IEnumerable<JobProfitabilityRow> query = _allRows;

            if (!string.Equals(CompanyFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.CompanyName, CompanyFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(SalesmanFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.Salesman, SalesmanFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(CustomerFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.CustomerName, CustomerFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Date range filter
            if (DateRangeFilter != "All")
            {
                var now = DateTime.Now;
                DateTime cutoff = DateRangeFilter switch
                {
                    "This Week" => now.AddDays(-(int)now.DayOfWeek),
                    "This Month" => new DateTime(now.Year, now.Month, 1),
                    "This Quarter" => new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1),
                    "This Year" => new DateTime(now.Year, 1, 1),
                    "Last 30 Days" => now.AddDays(-30),
                    "Last 90 Days" => now.AddDays(-90),
                    _ => DateTime.MinValue
                };
                if (cutoff > DateTime.MinValue)
                    query = query.Where(r => r.IssueDate >= cutoff);
            }

            string term = (SearchText ?? "").Trim();
            if (term.Length > 0)
            {
                query = query.Where(r =>
                    Utilities.SearchHelper.ContainsIgnoreCase(r.ProformaId, term) ||
                    Utilities.SearchHelper.ContainsIgnoreCase(r.CustomerName, term) ||
                    Utilities.SearchHelper.ContainsIgnoreCase(r.ProjectName, term));
            }

            var result = query
                .OrderByDescending(r => r.IssueDate)
                .ToList();

            Rows.ClearSilent();
            Rows.AddRangeSilent(result);
            Rows.RaiseReset();
            OnPropertyChanged(nameof(HasRows));

            RecalculateTotals(result);
            StatusMessage = BuildStatus(result);
        }

        private void RecalculateTotals(List<JobProfitabilityRow> rows)
        {
            // ONLY costed rows enter the totals. Uncosted (confirmed before
            // Step 9 existed) rows carry zero profit AND zero net now, so
            // even their exclusion from the sum is already reflected — but
            // the filter keeps the intent explicit: a job with no P&L data
            // must never dilute the portfolio margin or the loss count.
            var costedRows = rows.Where(r => r.IsCosted).ToList();

            TotalNet = Math.Round(costedRows.Sum(r => r.NetExVat), 2);
            TotalCost = Math.Round(costedRows.Sum(r => r.TotalCost), 2);
            TotalProfit = Math.Round(costedRows.Sum(r => r.Profit), 2);
            LossCount = rows.Count(r => r.IsLoss);

            // Overall portfolio margin uses EXACTLY the Step 9 per-PI
            // formula applied to the totals:
            //     Margin% = Profit ÷ NetExVat × 100
            // Step 9's own margin (ProfitLossStepViewModel) is
            // Profit / NetExVat * 100 rounded to one decimal; summing
            // Step 9's net and Step 9's profit first, then applying the
            // same division, is the portfolio-level equivalent.
            //
            // This is computed from the TOTALS, not averaged from the
            // individual margins — an average of percentages weights tiny
            // jobs the same as large ones. (Step 9 has no "overall"
            // concept: it values one PI, and its margin is exactly
            // Profit ÷ NetExVat — the same division used here.)
            //
            // Edge case: if TotalNet is very small relative to TotalProfit,
            // the margin can exceed ±100% (e.g., net=10, profit=-121 →
            // -1210%). This is mathematically correct and matches what
            // Step 9 would show for that single PI.
            OverallMarginPercent = TotalNet > 0m
                ? Math.Round((TotalProfit * 100m) / TotalNet, 1)
                : 0m;

            // Average Margin %: only the Margin % figures of the displayed
            // VALID rows (costed rows carry a real Step 9 margin; uncosted
            // rows have no margin and are excluded). Rows are never
            // weighted by Net/Cost/Profit amount, so this is a plain mean
            // of percentages. Zero valid rows → "—".
            AverageMarginCount = costedRows.Count;
            AverageMarginPercent = costedRows.Count > 0
                ? Math.Round(costedRows.Average(r => r.MarginPercent), 1)
                : 0m;

            OnPropertyChanged(nameof(IsPortfolioLoss));
        }

        private string BuildStatus(List<JobProfitabilityRow> rows)
        {
            if (_allRows.Count == 0)
                return "No PI-confirmed proforma invoices yet.";

            if (rows.Count == 0)
                return "No PI-confirmed jobs match the current filters.";

            int uncosted = rows.Count(r => !r.IsCosted);

            string basis = $"{rows.Count} of {_allRows.Count} PI-confirmed job(s) shown.";

            // Stale costings first: an overstated profit is more dangerous
            // than a blank one, because it looks like a real number.
            int stale = rows.Count(r => r.NeedsRecalculation);

            if (stale > 0)
            {
                basis += $"  ⚠ {stale} job(s) show OVERSTATED profit — costed " +
                         "before the inner-pane fix. Reopen each, recalculate " +
                         "Step 9, then save.";
            }

            if (uncosted > 0)
            {
                // The original wording blamed old invoices, but the usual
                // cause is different and far more common: Step 9 only fills
                // in when the tab is opened, so ANY invoice saved without
                // visiting Step 9 persists zero cost. Name both causes.
                basis += $" {uncosted} job(s) have no Profit & Loss data, so " +
                         "their cost, profit and margin are blank — open each " +
                         "invoice, visit Step 9 to recalculate, then save.";
            }

            return basis;
        }
    }
}
