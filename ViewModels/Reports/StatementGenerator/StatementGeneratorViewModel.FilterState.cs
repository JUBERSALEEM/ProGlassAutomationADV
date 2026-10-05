// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.FilterState.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// FilterState members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  FILTER FIELDS
        // ═════════════════════════════════════════════════════════════════

        private string _filterCompany = "";
        public string FilterCompany
        {
            get => _filterCompany;
            set
            {
                if (_filterCompany == value) return;
                _filterCompany = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterCustomer = "";
        public string FilterCustomer
        {
            get => _filterCustomer;
            set
            {
                if (_filterCustomer == value) return;
                _filterCustomer = value;
                OnPropertyChanged();
                // The project dropdown must reflect the chosen customer.
                RefreshProjectsForCustomer();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterProject = "";
        public string FilterProject
        {
            get => _filterProject;
            set
            {
                if (_filterProject == value) return;
                _filterProject = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterSalesman = "";
        public string FilterSalesman
        {
            get => _filterSalesman;
            set
            {
                if (_filterSalesman == value) return;
                _filterSalesman = value;
                OnPropertyChanged();
                // The customer dropdown must reflect the chosen salesman.
                RefreshCustomersForSalesman();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (_dateFrom == value) return;
                _dateFrom = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (_dateTo == value) return;
                _dateTo = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF OPTIONS
        // ═════════════════════════════════════════════════════════════════

        private bool _includeAttachments = true;
        public bool IncludeAttachments
        {
            get => _includeAttachments;
            set { if (_includeAttachments == value) return; _includeAttachments = value; OnPropertyChanged(); }
        }

        private bool _includeDnDetails;
        public bool IncludeDnDetails
        {
            get => _includeDnDetails;
            set { if (_includeDnDetails == value) return; _includeDnDetails = value; OnPropertyChanged(); }
        }

        private bool _includeBalanceAnalysis = true;
        public bool IncludeBalanceAnalysis
        {
            get => _includeBalanceAnalysis;
            set { if (_includeBalanceAnalysis == value) return; _includeBalanceAnalysis = value; OnPropertyChanged(); }
        }

        private bool _includeAgingChart = true;
        public bool IncludeAgingChart
        {
            get => _includeAgingChart;
            set { if (_includeAgingChart == value) return; _includeAgingChart = value; OnPropertyChanged(); }
        }

        private bool _includeInsertPlaceholders = true;
        public bool IncludeInsertPlaceholders
        {
            get => _includeInsertPlaceholders;
            set { if (_includeInsertPlaceholders == value) return; _includeInsertPlaceholders = value; OnPropertyChanged(); }
        }

        private bool _includeWarningsSummary = true;
        public bool IncludeWarningsSummary
        {
            get => _includeWarningsSummary;
            set { if (_includeWarningsSummary == value) return; _includeWarningsSummary = value; OnPropertyChanged(); }
        }

        // ── Auto-preview toggle ──────────────────────────────────────────

        private bool _autoPreviewEnabled = true;
        public bool AutoPreviewEnabled
        {
            get => _autoPreviewEnabled;
            set
            {
                if (_autoPreviewEnabled == value) return;
                _autoPreviewEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoPreviewStatusDisplay));

                if (_autoPreviewEnabled && HasFilter && !_isLoading)
                    SchedulePreview();
            }
        }

        public string AutoPreviewStatusDisplay =>
            _autoPreviewEnabled
                ? "Auto-preview  ·  400ms debounce"
                : "Auto-preview off  ·  Click Preview to update";

        // ═════════════════════════════════════════════════════════════════
        //  STATUS ITEMS + DROPDOWN SOURCES
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // Full client list (name + assigned salesman) kept so the Customer
        // dropdown can be filtered to only the customers of the selected
        // salesman. Loaded once in LoadMasterDataAsync.
        private List<SavedClient> _allClients = new();

        // Full company records (v3 branding) kept so the statement letterhead
        // can resolve the issuing company's logo synchronously at preview /
        // generation time. Loaded once in LoadMasterDataAsync.
        private List<SavedCompany> _allCompanies = new();

        // Full project list (name + owning customer) kept so the Project
        // dropdown can be filtered to only the projects of the selected
        // customer. Loaded once in LoadMasterDataAsync.
        private List<SavedProject> _allProjects = new();

        // ═════════════════════════════════════════════════════════════════
        //  CORE STATE
        // ═════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set
            {
                if (_isGenerating == value) return;
                _isGenerating = value;
                OnPropertyChanged();
                // [CHAT 22 v3] Notify ALL navigation gates — mid-run is locked
                NotifyAllNavigationStates();
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _hasPreview;
        public bool HasPreview
        {
            get => _hasPreview;
            set
            {
                if (_hasPreview == value) return;
                _hasPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MatchCountDisplay));
                OnPropertyChanged(nameof(MatchCount));
            }
        }

        public bool HasFilter =>
            !string.IsNullOrWhiteSpace(_filterCompany) ||
            !string.IsNullOrWhiteSpace(_filterCustomer) ||
            !string.IsNullOrWhiteSpace(_filterProject) ||
            !string.IsNullOrWhiteSpace(_filterSalesman) ||
            _dateFrom.HasValue ||
            _dateTo.HasValue ||
            StatusItems.Any(s => s.IsChecked);

        private StatementSnapshot? _previewSnapshot;
        public StatementSnapshot? PreviewSnapshot
        {
            get => _previewSnapshot;
            private set
            {
                _previewSnapshot = value;
                OnPropertyChanged();
                HasPreview = value != null;
                UpdateStatusItemCounts(value);
                OnPropertyChanged(nameof(MatchCountDisplay));
                OnPropertyChanged(nameof(MatchCount));
            }
        }

        public int MatchCount => _previewSnapshot?.InvoiceRows.Count ?? 0;

        public string MatchCountDisplay
        {
            get
            {
                if (_previewSnapshot == null)
                {
                    if (!HasFilter) return "Configure filters to see match count";
                    if (_isLoading) return "Searching…";
                    return "No matches yet";
                }
                int n = _previewSnapshot.InvoiceRows.Count;
                return n == 1 ? "1 invoice matches filters" : $"{n:N0} invoices match filters";
            }
        }

    }
}
