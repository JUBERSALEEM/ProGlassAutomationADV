// ═══════════════════════════════════════════════════════════════════════
// FILE: InquiryListViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.Inquiry
{
        public sealed partial class InquiryListViewModel : ViewModelBase, System.IDisposable
    {
            // ── Actions ────────────────────────────────────────────────────

        private async Task EditRowAsync(InquiryListRow row)
        {
            var detail = await _repository.GetDetailAsync(row.Id);
            await EditAsync(detail);
        }

        private async Task EditAsync(InquiryRecord? record)
        {
            if (EditRequested == null) { SetStatus("Inquiry editor is unavailable."); return; }
            if (await EditRequested(record)) ResetAndReload();
        }

        private async Task ConvertToPiAsync()
        {
            if (SelectedInquiry == null || CreateProformaRequested == null) return;
            var detail = await _repository.GetDetailAsync(SelectedInquiry.Id);
            if (detail == null) return;
            if (detail.ProformaInvoiceId.HasValue || !string.IsNullOrWhiteSpace(detail.ProformaNumber))
            {
                SetStatus($"PI {detail.ProformaNumber} already exists for this inquiry.");
                return;
            }
            await CreateProformaRequested(detail);
        }

        private async Task ChangeSelectedStatusAsync(string? next)
        {
            var row = SelectedInquiry;
            if (row == null || string.IsNullOrWhiteSpace(next)) return;
            try
            {
                if (!InquiryWorkflowService.CanTransition(row.Status, next))
                {
                    SetStatus($"An inquiry cannot move from {row.Status} to {next}.");
                    return;
                }
                bool changed = await _workflow.ChangeStatusAsync(
                    row.Id, row.Status, next, row.RowVersion);
                if (changed)
                {
                    SetStatus($"Inquiry {row.InquiryNumber} is now {InquiryStatuses.Normalize(next)}.");
                    Reload();
                }
            }
            catch (Exception ex) { SetStatus(ExceptionMessages.Deep(ex)); }
        }

        private async Task ExportAsync()
        {
            if (!Services.Audit.PermissionService.Require(Services.Audit.AppPermissions.ProformaExport, "export inquiries")) return;
            string? path = ExportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                int count = await ExportInquiriesToPathAsync(path);
                SetStatus($"Exported {count} inquiries to {System.IO.Path.GetFileName(path)}.");
            }
            catch (Exception ex) { SetStatus("Export failed: " + ExceptionMessages.Deep(ex)); }
        }

        // Shared export core: the filtered, pipeline-aware row fetch plus
        // the CSV write. ExportAsync targets the user's chosen path;
        // ShareAsync stages the SAME core into the share temp folder.
        private async Task<int> ExportInquiriesToPathAsync(string path)
        {
            // Export every row matching the current filter (paged fetch, bounded),
            // not just the visible page.
            var range = DateRange(SelectedDateRange, DateTime.Now);
            InquiryQuery Build(int page) => new()
            {
                Search = SearchText, Status = SelectedStatusKey.Length == 0 ? "All statuses" : SelectedStatusKey,
                SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                From = range.From, ToExclusive = range.ToExclusive,
                HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                Sort = SelectedSort, Page = page, PageSize = 200
            };
            var rows = new System.Collections.Generic.List<InquiryListProjection>();
            var result = await _repository.QueryAsync(Build(1));
            rows.AddRange(result.Items);
            while (result.HasNext && rows.Count < 5000)
            {
                result = await _repository.QueryAsync(Build(result.Page + 1));
                rows.AddRange(result.Items);
            }
            // Apply the same in-memory pipeline filter that LoadAsync
            // applies. Without this, a user who filtered to "PI only"
            // would export ALL inquiries regardless of the visible
            // filter — which is almost never what they want.
            var pipelineFilter = SelectedPipelineKind;
            if (pipelineFilter.HasValue)
            {
                int minStage = (int)pipelineFilter.Value;
                rows = rows.Where(x => x.Lifecycle.FurthestStageReached >= minStage).ToList();
            }
            await _io.ExportCsvAsync(path, rows);
            return rows.Count;
        }

        // ── Sharing Center (inquiries export CSV only — no rule lookup) ──
        private async Task ShareAsync()
        {
            if (!Services.Audit.PermissionService.Require(Services.Audit.AppPermissions.ProformaExport, "share inquiries")) return;

            var context = new ProGlassAutomation.Services.Sharing.ShareContext
            {
                DocumentTypeKey = "Inquiry",
                Title = "Inquiries",
                SalesmanName = SelectedSalesman ?? "",
                SourceModule = "Inquiry"
            };

            await ProGlassAutomation.Services.Sharing.ShareLauncher.GenerateAndShareAsync(
                context,
                async folder =>
                {
                    try
                    {
                        string path = System.IO.Path.Combine(folder,
                            $"Inquiries_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv");
                        int count = await ExportInquiriesToPathAsync(path);
                        if (count == 0)
                        {
                            SetStatus("There is nothing to share.");
                            return null;
                        }
                        return path;
                    }
                    catch (Exception ex)
                    {
                        SetStatus("Share failed: " + ExceptionMessages.Deep(ex));
                        return null;
                    }
                },
                _dialogs);
        }

        private async Task ImportAsync()
        {
            string? path = ImportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            // Cancel any in-flight import so a second click doesn't
            // double-parse the same file. Old token is disposed.
            _importCts?.Cancel();
            _importCts?.Dispose();
            _importCts = new CancellationTokenSource();
            var ct = _importCts.Token;
            try
            {
                // Pass the token so ParseCsvAsync can short-circuit a
                // huge / malicious file. (The service still falls back
                // to File.ReadAllLinesAsync internally; for a full
                // streaming fix see the audit notes. The token at least
                // lets a second-click user cancel an in-flight parse.)
                var parse = await _io.ParseCsvAsync(path, ct);
                if (parse.Rows.Count == 0)
                {
                    SetStatus(parse.SkippedInvalidRows > 0
                        ? $"Import contains no valid rows ({parse.SkippedInvalidRows} rows lacked customer or project)."
                        : "Import contains no rows.");
                    return;
                }
                if (ConfirmImportRequested?.Invoke(parse.Rows.Count) != true) return;
                int created = 0, failed = 0;
                foreach (var row in parse.Rows)
                {
                    if (ct.IsCancellationRequested) return;
                    try { await _workflow.CreateAsync(row); created++; }
                    catch (Services.Audit.PermissionDeniedException) { throw; }
                    catch (Exception) { failed++; }
                }
                string suffix = parse.SkippedInvalidRows > 0 ? $", {parse.SkippedInvalidRows} invalid skipped" : "";
                string dupes = parse.DuplicateNumbersRegenerated > 0
                    ? $", {parse.DuplicateNumbersRegenerated} duplicate numbers regenerated" : "";
                SetStatus($"Imported {created} inquiries{suffix}{dupes}{(failed > 0 ? $", {failed} failed" : "")}.");
                ResetAndReload();
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ExceptionMessages.Deep(ex)); }
            catch (OperationCanceledException) {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("catch suppressed");
            }
            catch (Exception ex) { SetStatus("Import failed: " + ExceptionMessages.Deep(ex)); }
        }

        private static (DateTime? From, DateTime? ToExclusive) DateRange(string value, DateTime now)
        {
            var today = now.Date;
            return value switch
            {
                "Today" => (today, today.AddDays(1)),
                "Yesterday" => (today.AddDays(-1), today),
                "Last 7 Days" => (today.AddDays(-6), today.AddDays(1)),
                "Last 30 Days" => (today.AddDays(-29), today.AddDays(1)),
                _ => (null, null) // "All time" — the honest default
            };
        }

        // ── Pagination & state ─────────────────────────────────────────

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                _currentPage = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }
        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }
        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); } }
        private string _errorText = "";
        public string ErrorText { get => _errorText; private set { _errorText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
        private string _statusText = "Loading…";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        private void SetStatus(string value) => StatusText = value;

        private void ResetFilters()
        {
            _searchText = ""; _selectedDateRange = "All time";
            _selectedSalesman = "All salesmen"; _selectedPreparedBy = "All prepared by";
            _selectedPriority = "All priorities"; _selectedSort = "Updated newest";
            _hasAttachmentsOnly = false;
            foreach (var tab in StatusTabs) tab.IsSelected = tab.Key.Length == 0;
            SelectedPipelineKind = null;
            OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(SelectedDateRange));
            OnPropertyChanged(nameof(SelectedSalesman)); OnPropertyChanged(nameof(SelectedPreparedBy));
            OnPropertyChanged(nameof(SelectedPriority)); OnPropertyChanged(nameof(SelectedSort));
            OnPropertyChanged(nameof(HasAttachmentsOnly)); OnPropertyChanged(nameof(HasActiveFilters));
            ResetAndReload();
        }
        public void Dispose()
        {
            // Best-effort CTS teardown — a cancel/dispose failure must not
            // crash navigation, but it must be observable (S-5: no silent
            // catches in production code).
            try { _loadCts?.Cancel(); _loadCts?.Dispose(); _loadCts = null; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[InquiryListViewModel] Dispose: load CTS teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { _debounceCts?.Cancel(); _debounceCts?.Dispose(); _debounceCts = null; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[InquiryListViewModel] Dispose: debounce CTS teardown failed: " + ExceptionMessages.Deep(ex)); }
            try { _importCts?.Cancel(); _importCts?.Dispose(); _importCts = null; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[InquiryListViewModel] Dispose: import CTS teardown failed: " + ExceptionMessages.Deep(ex)); }
        }
    }
}
