// ═══════════════════════════════════════════════════════════════════════
//  FILE: ViewModels/Specifications/SpecificationsCenterViewModel.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — Specifications Center (slice 3b)
//
//  WHAT THIS IS
//  ---------------------------------------------------------------------
//  A read-oriented LEDGER of every saved glass specification, laid out
//  like Sheet Store: KPI cards on top, filters, a list, and a detail
//  panel that opens when a row is clicked.
//
//  WHAT THIS IS NOT
//  It does not build or price specifications — Step 4 remains the only
//  place that happens. There is no "Add Specification" dialog, because a
//  spec is a DERIVED artefact: it only means something alongside the
//  pricing inputs that produced it. Offering a hand-entry form here would
//  let someone create a spec that Step 4 could never reproduce.
//
//  The Sheet Store relationship is READ-ONLY and exists purely to answer
//  "which sheet size was this quoted against". Nothing here writes to
//  Sheet Store, and nothing here re-prices a spec.
//
//  The only mutation offered is DELETE, for pruning mistakes out of the
//  catalogue, and it is permission-gated in the repository.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Specifications
{
    public class SpecificationsCenterViewModel : ViewModelBase, IDisposable
    {
        private readonly IDialogService? _dialogs;
        private readonly ISpecificationsRepository? _specs;

        // Full unfiltered set; Specifications is the filtered view of it.
        private List<SavedSpecification> _all = new();

        public SpecificationsCenterViewModel(
            IDialogService? dialogs,
            ISpecificationsRepository? specs)
        {
            _dialogs = dialogs;
            _specs = specs;

            InitializeCore();
        }

        // Parameterless ctor chains, matching the ERR-633EEB v2 pattern:
        // the body lives in InitializeCore() and is never duplicated.
        public SpecificationsCenterViewModel()
            : this(App.ServiceProvider?.GetService(typeof(IDialogService)) as IDialogService,
                   App.Specs)
        {
        }

        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            SelectSpecCommand = new RelayCommand(p => _ = SelectSpecAsync(p as SavedSpecification));
            CloseDetailCommand = new RelayCommand(_ => SelectedSpec = null);
            DeleteSpecCommand = new RelayCommand(p => _ = DeleteSpecAsync(p as SavedSpecification));
            ExportCsvCommand = new RelayCommand(_ => _ = ExportCsvAsync());
            ImportCsvCommand = new RelayCommand(_ => _ = ImportCsvAsync());
            ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync());
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync());

            // Live catalogue sync: Step 4 saving a spec updates this list
            // with no manual refresh, same channel Step 5's dropdown uses.
            SpecCatalogNotifier.Changed += OnCatalogChanged;

            _ = LoadAsync();
        }

        private void OnCatalogChanged(object? sender, EventArgs e)
        {
            // May arrive on a background thread (async repository writes).
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                _ = LoadAsync();
            else
                dispatcher.BeginInvoke(new Action(() => _ = LoadAsync()));
        }

        // ── Collections ──────────────────────────────────────────────────

        public BulkObservableCollection<SavedSpecification> Specifications { get; } = new();
        public ObservableCollection<SpecificationUsage> UsageHistory { get; } = new();

        /// <summary>
        /// Individual glass leaves of the selected spec, each with the Sheet
        /// Store size it matched. A DGU has two, and they can be different
        /// stock sizes — which is why a single width/height on the spec was
        /// never enough.
        /// </summary>
        public ObservableCollection<Models.ProformaInvoice.Specifications.SpecPane> Panes { get; } = new();

        public bool HasPanes => Panes.Count > 0;

        public ObservableCollection<string> GlassTypeOptions { get; } = new();
        public ObservableCollection<string> ColorOptions { get; } = new();

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Most used",
            "Recently used",
            "Newest first",
            "Name (A–Z)",
            "Price (high → low)"
        };

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ClearFiltersCommand { get; private set; } = null!;
        public ICommand SelectSpecCommand { get; private set; } = null!;
        public ICommand CloseDetailCommand { get; private set; } = null!;
        public ICommand DeleteSpecCommand { get; private set; } = null!;
        public ICommand ExportCsvCommand { get; private set; } = null!;
        public ICommand ImportCsvCommand { get; private set; } = null!;
        public ICommand ExportExcelCommand { get; private set; } = null!;
        public ICommand ExportPdfCommand { get; private set; } = null!;

        // ── Filters ──────────────────────────────────────────────────────

        private const string AllFilter = "All";

        private string _searchText = "";
        // NOTE: The XAML binding uses Delay=200 on the search TextBox,
        // which provides UI-level debouncing. The VM simply applies
        // filters when the value changes — no double-debounce needed.
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value ?? ""; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _glassTypeFilter = AllFilter;
        public string GlassTypeFilter
        {
            get => _glassTypeFilter;
            set { if (_glassTypeFilter == value) return; _glassTypeFilter = value ?? AllFilter; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _colorFilter = AllFilter;
        public string ColorFilter
        {
            get => _colorFilter;
            set { if (_colorFilter == value) return; _colorFilter = value ?? AllFilter; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _sortBy = "Most used";
        public string SortBy
        {
            get => _sortBy;
            set { if (_sortBy == value) return; _sortBy = value ?? "Most used"; OnPropertyChanged(); ApplyFilters(); }
        }

        // ── Selection / detail panel ─────────────────────────────────────

        private SavedSpecification? _selectedSpec;
        public SavedSpecification? SelectedSpec
        {
            get => _selectedSpec;
            set
            {
                _selectedSpec = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailOpen));
                OnPropertyChanged(nameof(DetailColumnWidth));
            }
        }

        public bool IsDetailOpen => _selectedSpec != null;

        /// <summary>Slide-in width for the detail pane; 0 collapses it.</summary>
        public GridLength DetailColumnWidth =>
            _selectedSpec != null ? new GridLength(420) : new GridLength(0);

        // ── Status ───────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

        public bool IsEmpty => Specifications.Count == 0 && !IsLoading;

        // ── KPI cards ────────────────────────────────────────────────────
        // Computed from the FILTERED list so the numbers always describe
        // what is actually on screen — a KPI that disagrees with the list
        // below it is worse than no KPI.

        public int TotalSpecs => Specifications.Count;

        public int TotalUses => _cachedTotalUses;

        public decimal AveragePricePerSqm => _cachedAvgPrice;

        public int LinkedSheetCount => _cachedLinkedSheet;

        public string MostUsedSpecName => _cachedMostUsed;

        // Cached KPI values computed in a single pass during RaiseKpis().
        // Previously each property did its own LINQ scan of the full list
        // (Sum + Average + Count + OrderByDescending.First = 4 passes).
        private int _cachedTotalUses;
        private decimal _cachedAvgPrice;
        private int _cachedLinkedSheet;
        private string _cachedMostUsed = "—";

        private void RaiseKpis()
        {
            // SINGLE-PASS KPI computation instead of 4 separate LINQ scans.
            int totalUses = 0;
            decimal priceSum = 0m;
            int linkedSheet = 0;
            int maxUsage = 0;
            string mostUsedName = "—";

            foreach (var s in Specifications)
            {
                totalUses += s.UsageCount;
                priceSum += s.PricePerSqm;
                if (s.HasSheetMatch) linkedSheet++;
                if (s.UsageCount > maxUsage)
                {
                    maxUsage = s.UsageCount;
                    mostUsedName = s.SpecName ?? "—";
                }
            }

            _cachedTotalUses = totalUses;
            _cachedAvgPrice = Specifications.Count == 0
                ? 0m
                : Math.Round(priceSum / Specifications.Count, 2);
            _cachedLinkedSheet = linkedSheet;
            _cachedMostUsed = mostUsedName;

            OnPropertyChanged(nameof(TotalSpecs));
            OnPropertyChanged(nameof(TotalUses));
            OnPropertyChanged(nameof(AveragePricePerSqm));
            OnPropertyChanged(nameof(LinkedSheetCount));
            OnPropertyChanged(nameof(MostUsedSpecName));
            OnPropertyChanged(nameof(IsEmpty));
        }

        // ── Load / filter ────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            if (_specs == null)
            {
                StatusMessage = "Specifications repository unavailable.";
                return;
            }

            try
            {
                IsLoading = true;

                var rows = await _specs.GetAllAsync();
                _all = rows ?? new List<SavedSpecification>();

                RebuildFilterOptions();
                ApplyFilters();

                StatusMessage = "";
            }
            catch (Exception ex)
            {
                // Never throws into the shell: an unreadable catalogue shows
                // an empty, explained list rather than taking the app down.
                AppLogService.Warning(
                    $"Specifications Center load failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "Load" });
                StatusMessage = "Could not load specifications.";
                _all = new List<SavedSpecification>();
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private void RebuildFilterOptions()
        {
            string keptType = _glassTypeFilter;
            string keptColor = _colorFilter;

            GlassTypeOptions.Clear();
            GlassTypeOptions.Add(AllFilter);
            foreach (var t in _all.Select(s => s.GlassType)
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(t => t))
                GlassTypeOptions.Add(t);

            ColorOptions.Clear();
            ColorOptions.Add(AllFilter);
            foreach (var c in _all.Select(s => s.Color)
                                  .Where(c => !string.IsNullOrWhiteSpace(c))
                                  .Select(c => c!.Trim())
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(c => c))
                ColorOptions.Add(c);

            // Preserve the active filter if it still exists, so a background
            // refresh does not silently reset what the user is looking at.
            _glassTypeFilter = GlassTypeOptions.Contains(keptType) ? keptType : AllFilter;
            _colorFilter = ColorOptions.Contains(keptColor) ? keptColor : AllFilter;
            OnPropertyChanged(nameof(GlassTypeFilter));
            OnPropertyChanged(nameof(ColorFilter));
        }

        private void ApplyFilters()
        {
            IEnumerable<SavedSpecification> q = _all;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string term = _searchText.Trim();
                q = q.Where(s =>
                    (s.SpecName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.Color ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.ProformaId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(_glassTypeFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
                q = q.Where(s => string.Equals(s.GlassType, _glassTypeFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(_colorFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
                q = q.Where(s => string.Equals((s.Color ?? "").Trim(), _colorFilter, StringComparison.OrdinalIgnoreCase));

            q = _sortBy switch
            {
                "Recently used"      => q.OrderByDescending(s => s.LastUsedAt),
                "Newest first"       => q.OrderByDescending(s => s.CreatedAt),
                "Name (A–Z)"         => q.OrderBy(s => s.SpecName),
                "Price (high → low)" => q.OrderByDescending(s => s.PricePerSqm),
                _                    => q.OrderByDescending(s => s.UsageCount)
                                          .ThenByDescending(s => s.LastUsedAt)
            };

            // PERFORMANCE: AddRangeSilent + single RaiseReset fires ONE
            // CollectionChanged(Reset) instead of N+1 events (1 Clear +
            // N Add). With 500+ specs this prevents visible jank during
            // filtering.
            Specifications.ClearSilent();
            Specifications.AddRangeSilent(q);
            Specifications.RaiseReset();

            RaiseKpis();
        }

        private void ClearFilters()
        {
            _searchText = "";
            _glassTypeFilter = AllFilter;
            _colorFilter = AllFilter;
            _sortBy = "Most used";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(GlassTypeFilter));
            OnPropertyChanged(nameof(ColorFilter));
            OnPropertyChanged(nameof(SortBy));

            ApplyFilters();
        }

        // ── Row click → detail + usage history ───────────────────────────

        private async Task SelectSpecAsync(SavedSpecification? spec)
        {
            SelectedSpec = spec;
            UsageHistory.Clear();
            Panes.Clear();
            OnPropertyChanged(nameof(HasPanes));

            if (spec == null || _specs == null) return;

            // Resolve the panes first so the sidebar fills in immediately.
            // Uses stored PanesJson when present and otherwise derives them
            // from the spec NAME, which is what makes every pre-existing
            // specification display sizes without a data backfill.
            try
            {
                var panes = await Services.Specifications.SpecPaneResolver
                    .ResolveAsync(spec);

                foreach (var pane in panes) Panes.Add(pane);
                OnPropertyChanged(nameof(HasPanes));
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecificationsCenter.Panes] {ex.Message}");
            }

            try
            {
                var history = await _specs.GetUsageHistoryAsync(spec.Id);
                foreach (var u in history) UsageHistory.Add(u);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SpecificationsCenter.History] {ex.Message}");
            }
            finally
            {
                RebuildPriceBands();
                OnPropertyChanged(nameof(UsageCountDisplay));
            }
        }

        public string UsageCountDisplay =>
            UsageHistory.Count == 1 ? "1 use" : $"{UsageHistory.Count} uses";

        // ── Price variation across proformas ─────────────────────────────
        //
        // You were unsure whether one spec can carry different prices. It
        // can, and it routinely does: SaveOrUpdateAsync OVERWRITES
        // PricePerSqm on every Step 4 apply, so the catalogue row holds only
        // the LATEST rate while each usage row holds what that invoice was
        // actually quoted at. Showing only the latest would misrepresent
        // history, so the detail pane lists the distinct rates with the
        // proformas that used them.

        public ObservableCollection<SpecPriceBand> PriceBands { get; } = new();

        private void RebuildPriceBands()
        {
            PriceBands.Clear();

            foreach (var g in UsageHistory
                         .GroupBy(u => u.PricePerSqm)
                         .OrderByDescending(g => g.Key))
            {
                var pis = g.Select(u => u.ProformaDisplay)
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList();

                PriceBands.Add(new SpecPriceBand
                {
                    PricePerSqm = g.Key,
                    UseCount = g.Count(),
                    ProformaList = pis.Count == 0 ? "—" : string.Join(", ", pis)
                });
            }

            OnPropertyChanged(nameof(HasPriceVariation));
        }

        /// <summary>True when this spec was quoted at more than one rate.</summary>
        public bool HasPriceVariation => PriceBands.Count > 1;

        // ── CSV export / import ──────────────────────────────────────────

        private async Task ExportCsvAsync()
        {
            if (Specifications.Count == 0)
            {
                _dialogs?.Info("There is nothing to export.", "Specifications");
                return;
            }

            try
            {
                // Exports the FILTERED list, matching what is on screen --
                // exporting hidden rows the user has filtered out is a
                // surprise, not a feature.
                var layout = new Services.Specifications.Export
                    .SpecificationsCsvLayout(Specifications.ToList());

                var engine = new Services.Export.Csv.CsvExportEngine();
                var result = await engine.ExportAsync(
                    layout,
                    new Services.Export.Core.ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = false,
                        SuggestedFileName =
                            $"Specifications_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                    });

                StatusMessage = result != null && result.Success
                    ? $"Exported {Specifications.Count} specifications."
                    : "Export cancelled.";
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specifications CSV export failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "ExportCsv" });
                _dialogs?.Error("Export failed.\n\n" + ex.Message, "Specifications");
            }
        }

        private async Task ExportExcelAsync()
        {
            if (Specifications.Count == 0)
            {
                _dialogs?.Info("There is nothing to export.", "Specifications");
                return;
            }

            try
            {
                // Pull ALL usage rows so the workbook's second sheet can
                // show the per-proforma price history, which is the part a
                // flat catalogue export cannot express.
                List<SpecificationUsage> usage = new();
                if (_specs != null)
                {
                    try { usage = await _specs.GetRecentUsageAsync(5000); }
                    catch (Exception ux)
                    {
                        AppLogService.Debug($"[SpecCenter.ExcelUsage] {ux.Message}");
                    }
                }

                var layout = new Services.Specifications.Export
                    .SpecificationsExcelLayout(Specifications.ToList(), usage);

                var engine = new Services.Export.Excel.ExcelExportEngine();
                var result = await engine.ExportAsync(
                    layout,
                    new Services.Export.Core.ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = false,
                        SuggestedFileName =
                            $"Specifications_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                    });

                StatusMessage = result != null && result.Success
                    ? $"Exported {Specifications.Count} specifications to Excel."
                    : "Export cancelled.";
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specifications Excel export failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "ExportExcel" });
                _dialogs?.Error("Excel export failed.\n\n" + ex.Message, "Specifications");
            }
        }

        private async Task ExportPdfAsync()
        {
            if (Specifications.Count == 0)
            {
                _dialogs?.Info("There is nothing to export.", "Specifications");
                return;
            }

            try
            {
                var layout = new Services.Specifications.Export
                    .SpecificationsPdfLayout(Specifications.ToList());

                var engine = new Services.Export.Pdf.PdfExportEngine();
                var result = await engine.ExportAsync(
                    layout,
                    new Services.Export.Core.ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = false,
                        SuggestedFileName =
                            $"Specifications_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                    });

                StatusMessage = result != null && result.Success
                    ? $"Exported {Specifications.Count} specifications to PDF."
                    : "Export cancelled.";
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specifications PDF export failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "ExportPdf" });
                _dialogs?.Error("PDF export failed.\n\n" + ex.Message, "Specifications");
            }
        }

        private async Task ImportCsvAsync()
        {
            if (_specs == null) return;

            try
            {
                var picker = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import specifications from CSV",
                    Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
                    CheckFileExists = true
                };
                if (picker.ShowDialog() != true) return;

                var rows = ParseCsv(picker.FileName);
                if (rows.Count == 0)
                {
                    _dialogs?.Warning(
                        "No usable rows were found. The file needs a header "
                        + "row with at least GlassType, SpecName and "
                        + "PricePerSqm.",
                        "Import specifications");
                    return;
                }

                var (ins, upd, skip) = await _specs.ImportAsync(rows);

                StatusMessage =
                    $"Imported — {ins} added, {upd} updated, {skip} skipped.";

                _dialogs?.Info(StatusMessage, "Import complete");
                // Repository raises SpecCatalogNotifier on success, which
                // reloads this list automatically.
            }
            catch (Services.Audit.PermissionDeniedException denied)
            {
                _dialogs?.Warning(denied.UserMessage, "Permission denied");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specifications CSV import failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "ImportCsv" });
                _dialogs?.Error("Import failed.\n\n" + ex.Message, "Specifications");
            }
        }

        /// <summary>
        /// Header-driven CSV parse: columns are located BY NAME, so a file
        /// with reordered or extra columns still imports correctly. Rows
        /// that cannot be parsed are dropped here and counted as skipped by
        /// the repository rather than aborting the whole import.
        /// </summary>
        private static List<SavedSpecification> ParseCsv(string path)
        {
            var result = new List<SavedSpecification>();
            var lines = System.IO.File.ReadAllLines(path);
            if (lines.Length < 2) return result;

            var header = SplitCsvLine(lines[0]);
            int Idx(string name) => header.FindIndex(h =>
                string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));

            int iType = Idx("GlassType"), iName = Idx("SpecName"), iPrice = Idx("PricePerSqm");
            if (iType < 0 || iName < 0 || iPrice < 0) return result;

            int iColor = Idx("Color"), iThk = Idx("ThicknessMm");
            int iW = Idx("SheetWidthMm"), iH = Idx("SheetHeightMm");
            int iCat = Idx("Category"), iPi = Idx("ProformaId");

            string? Get(List<string> f, int i) =>
                i >= 0 && i < f.Count ? f[i].Trim() : null;

            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                var f = SplitCsvLine(lines[r]);

                decimal.TryParse(Get(f, iPrice), out decimal price);
                int.TryParse(Get(f, iThk), out int thk);
                int.TryParse(Get(f, iW), out int w);
                int.TryParse(Get(f, iH), out int h);

                result.Add(new SavedSpecification
                {
                    GlassType = Get(f, iType) ?? "",
                    SpecName = Get(f, iName) ?? "",
                    PricePerSqm = price,
                    Color = Get(f, iColor),
                    ThicknessMm = thk,
                    SheetWidthMm = w,
                    SheetHeightMm = h,
                    Category = Get(f, iCat),
                    ProformaId = Get(f, iPi)
                });
            }

            return result;
        }

        // ── IDisposable — detach static event subscription ────────────
        // SpecCatalogNotifier.Changed is a static event that outlives
        // any single VM instance. Without detaching, the VM leaks until
        // the app exits (or until the next SpecCatalogNotifier.Changed
        // fires and tries to call a method on a dead VM).
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { SpecCatalogNotifier.Changed -= OnCatalogChanged; }
            catch { /* best-effort */ }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Minimal RFC-4180 splitter: handles quoted fields and escaped
        /// doubled quotes, which is what Excel emits for names containing
        /// commas (spec names routinely do: "6mm Clear FT, polished").
        /// </summary>
        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }

            fields.Add(sb.ToString());
            return fields;
        }

        // ── Delete ───────────────────────────────────────────────────────

        private async Task DeleteSpecAsync(SavedSpecification? spec)
        {
            if (spec == null || _specs == null) return;

            bool confirmed = _dialogs?.Confirm(
                $"Delete the specification \"{spec.SpecName}\"?\n\n"
                + "Existing invoices keep the price they were quoted at. "
                + "This only removes it from the catalogue and the Step 5 "
                + "dropdown.",
                "Delete specification") ?? false;

            if (!confirmed) return;

            try
            {
                await _specs.DeleteAsync(spec.Id);
                SelectedSpec = null;
                StatusMessage = $"Deleted \"{spec.SpecName}\".";
                // Repository raises SpecCatalogNotifier.Changed, which
                // reloads this list — no manual refresh needed.
            }
            catch (Services.Audit.PermissionDeniedException denied)
            {
                _dialogs?.Warning(denied.UserMessage, "Permission denied");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specification delete failed: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "Delete" });
                _dialogs?.Error("Could not delete that specification.", "Specifications");
            }
        }
    }
}
