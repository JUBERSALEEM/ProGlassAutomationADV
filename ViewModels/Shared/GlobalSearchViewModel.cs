// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Shared/GlobalSearchViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — top-bar global search (PHASE E)
//
// WHAT IT IS
//   One search box in the title bar that reaches across every module:
//   sheets, proformas, job orders, deliveries, tax invoices, companies
//   and clients. Picking a result navigates to the owning module.
//
// DESIGN RULES
//   • READ-ONLY. It queries the existing repositories through their
//     public GetAll/Search APIs and never writes anything.
//   • Minimum 2 characters — a one-letter query would scan every table
//     and return noise.
//   • Results capped at MaxResults (25) so a query like "a" cannot
//     freeze the UI building thousands of rows.
//   • Matching is OrdinalIgnoreCase Contains across the fields a user
//     would actually type (ids, customer, project, colour...).
//   • Navigation is INJECTED (Action<string>) rather than referencing
//     MainViewModel, so this VM stays testable and dependency-free.
//   • SearchAsync never throws: a module whose repository is unavailable
//     is skipped, the rest still return.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Shared
{
    public class GlobalSearchViewModel : ViewModelBase
    {
        /// <summary>Below this length the search is not run at all.</summary>
        public const int MinSearchLength = 2;

        /// <summary>Hard cap on rendered results.</summary>
        public const int MaxResults = 25;

        // Module filter labels — also the values shown in the ComboBox.
        public const string ModuleAll = "All Modules";
        public const string ModuleSheetStore = "Sheet Store";
        public const string ModuleProforma = "Proforma Invoice";
        public const string ModuleJobOrders = "Job Orders";
        public const string ModuleDeliveries = "Deliveries";
        public const string ModuleTaxInvoices = "Tax Invoices";
        public const string ModuleCompanies = "Companies";
        public const string ModuleClients = "Clients";

        /// <summary>Injected navigation callback (MainViewModel.Navigate).</summary>
        private readonly Action<string>? _navigate;

        /// <summary>Filter options for the module ComboBox.</summary>
        public ObservableCollection<string> ModuleOptions { get; } = new()
        {
            ModuleAll,
            ModuleSheetStore,
            ModuleProforma,
            ModuleJobOrders,
            ModuleDeliveries,
            ModuleTaxInvoices,
            ModuleCompanies,
            ModuleClients
        };

        private string _selectedModule = ModuleAll;
        /// <summary>Active module filter.</summary>
        public string SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (_selectedModule == value) return;
                _selectedModule = value ?? ModuleAll;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(FireSearchAsync, "GlobalSearch");
            }
        }

        private string _searchText = "";
        /// <summary>Query text; searches run at MinSearchLength or more.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? "";
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(FireSearchAsync, "GlobalSearch");
            }
        }

        /// <summary>Capped result set.</summary>
        public ObservableCollection<GlobalSearchResult> Results { get; } = new();

        private bool _isOpen;
        /// <summary>Drives the results Popup.</summary>
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen == value) return;
                _isOpen = value;
                OnPropertyChanged();
            }
        }

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

        /// <summary>True when a completed search produced nothing.</summary>
        public bool HasNoResults =>
            Results.Count == 0 &&
            (SearchText ?? "").Trim().Length >= MinSearchLength;

        private string _statusMessage = "";
        /// <summary>"12 results" / "No matches" / hint text.</summary>
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

        private GlobalSearchResult? _selectedResult;
        /// <summary>
        /// Two-way bound to the popup ListBox. Setting it to a real hit
        /// navigates; the ListBox clears it back to null afterwards.
        /// </summary>
        public GlobalSearchResult? SelectedResult
        {
            get => _selectedResult;
            set
            {
                _selectedResult = value;
                OnPropertyChanged();

                if (value != null)
                    OpenResult(value);
            }
        }

        /// <summary>Navigates to the module owning a result.</summary>
        public ICommand SelectResultCommand { get; }

        /// <summary>Clears the query and closes the popup.</summary>
        public ICommand ClearCommand { get; }

        public GlobalSearchViewModel(Action<string>? navigate = null)
        {
            _navigate = navigate;

            SelectResultCommand = new RelayCommand(param =>
            {
                if (param is GlobalSearchResult hit) OpenResult(hit);
            });

            ClearCommand = new RelayCommand(_ =>
            {
                SearchText = "";
                IsOpen = false;
            });

            Results.CollectionChanged += (_, __) =>
                OnPropertyChanged(nameof(HasNoResults));
        }

        private void OpenResult(GlobalSearchResult hit)
        {
            IsOpen = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(hit.NavigateTarget))
                    _navigate?.Invoke(hit.NavigateTarget);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GlobalSearch] navigate failed: {ex.Message}");
            }

            // Reset so re-picking the same row navigates again.
            _selectedResult = null;
            OnPropertyChanged(nameof(SelectedResult));
        }

        private async Task FireSearchAsync()
        {
            await SearchAsync().ConfigureAwait(true);
        }

        // ══════════════════════════════════════════════════════════════
        //  SEARCH
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs the query across the selected modules. Never throws; a
        /// module whose repository is unavailable is simply skipped.
        /// </summary>
        public async Task SearchAsync()
        {
            string term = (SearchText ?? "").Trim();

            if (term.Length < MinSearchLength)
            {
                Results.Clear();
                IsOpen = false;
                StatusMessage = $"Type at least {MinSearchLength} characters.";
                OnPropertyChanged(nameof(HasNoResults));
                return;
            }

            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var hits = new List<GlobalSearchResult>();

                if (Wants(ModuleSheetStore)) await AddSheetsAsync(hits, term);
                if (Wants(ModuleProforma)) await AddProformasAsync(hits, term);
                if (Wants(ModuleJobOrders)) await AddJobOrdersAsync(hits, term);
                if (Wants(ModuleDeliveries)) await AddDeliveriesAsync(hits, term);
                if (Wants(ModuleTaxInvoices)) await AddTaxInvoicesAsync(hits, term);
                if (Wants(ModuleCompanies)) await AddCompaniesAsync(hits, term);
                if (Wants(ModuleClients)) await AddClientsAsync(hits, term);

                Results.Clear();
                foreach (var hit in hits.Take(MaxResults)) Results.Add(hit);

                StatusMessage = Results.Count == 0
                    ? "No matches."
                    : hits.Count > MaxResults
                        ? $"Showing first {MaxResults} of {hits.Count} matches."
                        : $"{Results.Count} result(s).";

                IsOpen = true;
                OnPropertyChanged(nameof(HasNoResults));
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GlobalSearch] search failed: {ex.Message}");
                StatusMessage = "Search failed — some modules may be unavailable.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool Wants(string module) =>
            SelectedModule == ModuleAll || SelectedModule == module;

        /// <summary>Case-insensitive substring test used by every query.</summary>
        private static bool Hit(string? value, string term) =>
            (value ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Per-module queries ────────────────────────────────────────────

        private static async Task AddSheetsAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                var repo = new Services.SheetStore.SheetStoreRepository();
                var sheets = await repo.GetAllAsync().ConfigureAwait(true);

                foreach (var s in sheets.Where(s =>
                             Hit(s.Color, term) ||
                             Hit(s.Category, term) ||
                             Hit(s.Supplier, term) ||
                             Hit(s.ThicknessDisplay, term) ||
                             Hit(s.SizeDisplay, term))
                         .Take(MaxResults))
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleSheetStore,
                        NavigateTarget = "SheetStore",
                        Glyph = "\uE7B8",
                        Title = $"{s.ThicknessDisplay} {s.Color} · {s.SizeDisplay}",
                        Subtitle = $"{s.Category} · {s.Supplier} · balance {s.Balance}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleSheetStore, ex); }
        }

        private static async Task AddProformasAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.Invoices == null) return;

                // PERF: bounded, DB-side search - only the top matches are
                // returned, so the FullSnapshotJson blobs are never loaded.
                var rows = await App.Invoices
                    .SearchAsync(term, MaxResults).ConfigureAwait(true);

                foreach (var pi in rows)
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleProforma,
                        NavigateTarget = "ProformaInvoice",
                        Glyph = "\uE8A5",
                        Title = pi.ProformaId,
                        Subtitle = $"{pi.CustomerName} · {pi.ProjectName} · {pi.Status}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleProforma, ex); }
        }

        private static async Task AddJobOrdersAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.Jobs == null) return;

                // PERF: bounded, DB-side search - only the top matches are
                // returned, so the FullSnapshotJson blobs are never loaded.
                var rows = await App.Jobs
                    .SearchAsync(term, MaxResults).ConfigureAwait(true);

                foreach (var jo in rows)
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleJobOrders,
                        NavigateTarget = "JobOrder",
                        Glyph = "\uE8C8",
                        Title = jo.JobOrderId,
                        Subtitle = $"{jo.CustomerName} · {jo.ProjectName} · {jo.Status}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleJobOrders, ex); }
        }

        private static async Task AddDeliveriesAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.DeliveryNotes == null) return;
                var list = await App.DeliveryNotes.GetAllAsync().ConfigureAwait(true);

                foreach (var dn in list.Where(d =>
                             Hit(d.DeliveryNoteId, term) ||
                             Hit(d.CustomerName, term) ||
                             Hit(d.ProjectName, term) ||
                             Hit(d.JobOrderId, term))
                         .Take(MaxResults))
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleDeliveries,
                        NavigateTarget = "DeliveryNote",
                        Glyph = "\uE7F4",
                        Title = dn.DeliveryNoteId,
                        Subtitle = $"{dn.CustomerName} · {dn.ProjectName} · {dn.Status}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleDeliveries, ex); }
        }

        private static async Task AddTaxInvoicesAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.TaxInvoices == null) return;
                var list = await App.TaxInvoices.GetAllAsync().ConfigureAwait(true);

                foreach (var ti in list.Where(t =>
                             Hit(t.InvoiceNumber, term) ||
                             Hit(t.CustomerName, term) ||
                             Hit(t.ProjectName, term) ||
                             Hit(t.ProformaId, term))
                         .Take(MaxResults))
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleTaxInvoices,
                        NavigateTarget = "TaxInvoice",
                        Glyph = "\uE7BF",
                        Title = ti.InvoiceNumber,
                        Subtitle = $"{ti.CustomerName} · {ti.ProjectName} · {ti.Status}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleTaxInvoices, ex); }
        }

        private static async Task AddCompaniesAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.MasterData == null) return;
                var list = await App.MasterData.GetAllCompaniesAsync()
                    .ConfigureAwait(true);

                foreach (var c in list.Where(c =>
                             Hit(c.CompanyName, term) ||
                             Hit(c.CompanyTrn, term) ||
                             Hit(c.CompanyLocation, term))
                         .Take(MaxResults))
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleCompanies,
                        NavigateTarget = "CompanyCenter",
                        Glyph = "\uE822",
                        Title = c.CompanyName,
                        Subtitle = $"TRN {c.CompanyTrn} · {c.CompanyLocation}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleCompanies, ex); }
        }

        private static async Task AddClientsAsync(
            List<GlobalSearchResult> hits, string term)
        {
            try
            {
                if (App.MasterData == null) return;
                var list = await App.MasterData.GetAllClientsAsync()
                    .ConfigureAwait(true);

                foreach (var c in list.Where(c =>
                             Hit(c.CustomerName, term) ||
                             Hit(c.CustomerTrn, term) ||
                             Hit(c.CustomerReferenceCode, term) ||
                             Hit(c.SalesRepresentative, term))
                         .Take(MaxResults))
                {
                    hits.Add(new GlobalSearchResult
                    {
                        ModuleLabel = ModuleClients,
                        NavigateTarget = "ProformaInvoice",
                        Glyph = "\uE716",
                        Title = c.CustomerName,
                        Subtitle = $"TRN {c.CustomerTrn} · rep {c.SalesRepresentative}"
                    });
                }
            }
            catch (Exception ex) { Skip(ModuleClients, ex); }
        }

        private static void Skip(string module, Exception ex) =>
            AppLogService.Debug($"[GlobalSearch] {module} skipped: {ex.Message}");
    }
}
