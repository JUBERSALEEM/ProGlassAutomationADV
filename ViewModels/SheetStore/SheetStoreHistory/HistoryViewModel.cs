using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Audit;   // PermissionService / AppPermissions / AuditService

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs = null!;

        // ERR-633EEB hardening: this DI overload used to leave every
        // command and collection NULL (all initialization lived in the
        // parameterless ctor). The container always picks the GREEDIEST
        // resolvable ctor, so DI-built instances were half-constructed.
        // It now chains into the fully-initializing ctor.
        public HistoryViewModel(IDialogService? dialogs) : this(dialogs, initialize: true)
        {
        }

        private readonly ISheetHistoryRepository _repo = new SheetHistoryRepository();
        private bool _isLoading;

        public ObservableCollection<SheetHistory> Entries { get; } = new();

        // ============ FILTERS ============

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        private string _filterAction = "All";
        public string FilterAction
        {
            get => _filterAction;
            set
            {
                if (_filterAction == value) return;
                _filterAction = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (_fromDate == value) return;
                _fromDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (_toDate == value) return;
                _toDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            }
        }

        public bool HasDateFilter => FromDate.HasValue || ToDate.HasValue;

        // ============ STATS ============

        public int TotalEntries => Entries.Count;
        public bool HasEntries => Entries.Count > 0;
        public bool IsEmpty => Entries.Count == 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ============ COMMANDS ============

        public ICommand RefreshCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ClearDatesCommand { get; }

        /// <summary>
        /// Undo a mistaken stock movement (2026-09-06). Writes a
        /// compensating entry — see SheetHistoryRepository
        /// .ReverseMovementAsync for why the original row is never
        /// deleted or edited.
        /// </summary>
        public ICommand UndoMovementCommand { get; }

        /// <summary>
        /// Raised after a successful undo so the Sheet Store grid behind
        /// this dialog reloads — otherwise the restored stock would not
        /// appear until the operator reopened the module.
        /// </summary>
        public event Action? MovementReversed;

        public Action? CloseAction { get; set; }

        public HistoryViewModel() : this(null, initialize: true)
        {
        }

        private HistoryViewModel(IDialogService? dialogs, bool initialize)
        {
            _dialogs = dialogs
                ?? App.ServiceProvider?.GetService<IDialogService>()
                ?? new DialogService();
            _ = initialize; // discriminator only — keeps the two ctors distinct

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());
            ClearDatesCommand = new RelayCommand(_ =>
            {
                _fromDate = null;
                _toDate = null;
                OnPropertyChanged(nameof(FromDate));
                OnPropertyChanged(nameof(ToDate));
                OnPropertyChanged(nameof(HasDateFilter));
                _ = LoadAsync();
            });

            UndoMovementCommand = new RelayCommand(
                param => AsyncUtility.SafeFireAndForget(
                    () => UndoMovementAsync(param as SheetHistory), "SheetStore Undo"),
                param => param is SheetHistory h && h.CanReverse);

            _ = LoadAsync();
        }

        /// <summary>
        /// Confirms, then asks the repository for a COMPENSATING entry.
        ///
        /// The UI never computes the stock adjustment itself — it names
        /// the row to undo and the repository re-validates every guard
        /// server-side (already reversed / not a movement / would drive a
        /// counter negative) inside one transaction. That way a stale
        /// grid cannot double-undo by clicking twice.
        /// </summary>
        private async Task UndoMovementAsync(SheetHistory? entry)
        {
            if (entry == null || !entry.CanReverse) return;

            if (!PermissionService.Require(
                    AppPermissions.SheetStoreEdit, "undo a stock movement"))
                return;

            string what = entry.Action == "Used" ? "consumption" : "purchase";
            var confirm = _dialogs.Show(
                $"Undo this {what}?\n\n" +
                $"{entry.SheetDescription}\n" +
                $"{entry.QuantityChanged} sheet(s) on {entry.TimestampDisplay}\n\n" +
                "The original entry is KEPT and marked reversed — a new " +
                "compensating entry restores the stock, so the audit trail " +
                "still shows what happened.",
                "Undo stock movement",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;

                var compensating = await _repo.ReverseMovementAsync(
                    entry.Id, reason: string.Empty);

                if (compensating == null)
                {
                    _dialogs.Show(
                        "This entry can no longer be undone. It may already " +
                        "have been reversed from another window.",
                        "Undo not possible",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    try
                    {
                        AuditService.Log(
                            AuditSections.SheetStore, "Stock movement reversed",
                            $"{entry.SheetDescription} — undo of #{entry.Id} " +
                            $"({entry.Action} {entry.QuantityChanged})",
                            "SHEET", entry.SheetId.ToString());
                    }
                    catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }

                    MovementReversed?.Invoke();
                }
            }
            catch (InvalidOperationException ex)
            {
                // Deliberate refusal from the repository (e.g. undoing a
                // purchase whose sheets are already consumed). This is a
                // business answer, not a crash — show the reason verbatim.
                _dialogs.Show(ex.Message, "Undo not possible",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    "Could not undo this movement:\n\n" + ExceptionMessages.Deep(ex),
                    LocalizationService.Instance.T("Msg.CapError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                await LoadAsync();
            }
        }

        private async Task LoadAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                IsBusy = true;

                var all = await _repo.GetAllAsync();

                // Filter by action
                if (FilterAction != "All" && !string.IsNullOrEmpty(FilterAction))
                    all = all.Where(h => h.Action == FilterAction).ToList();

                // Filter by date range
                if (FromDate.HasValue)
                    all = all.Where(h => h.Timestamp.Date >= FromDate.Value.Date).ToList();

                if (ToDate.HasValue)
                    all = all.Where(h => h.Timestamp.Date <= ToDate.Value.Date).ToList();

                // Filter by search
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var q = SearchText.Trim().ToLower();
                    all = all.Where(h =>
                        (h.SheetDescription ?? "").ToLower().Contains(q) ||
                        (h.Details ?? "").ToLower().Contains(q) ||
                        (h.UserName ?? "").ToLower().Contains(q)
                    ).ToList();
                }

                Entries.Clear();
                foreach (var e in all)
                    Entries.Add(e);

                OnPropertyChanged(nameof(TotalEntries));
                OnPropertyChanged(nameof(HasEntries));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    "Failed to load history:\n\n" + ExceptionMessages.Deep(ex),
                    LocalizationService.Instance.T("Msg.CapError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                IsBusy = false;
            }
        }
    }
}