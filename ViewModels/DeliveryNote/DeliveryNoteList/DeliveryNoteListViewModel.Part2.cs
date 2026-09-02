// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteListViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList.JobOrderPicker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
        public partial class DeliveryNoteListViewModel : ViewModelBase, System.IDisposable
    {
            private async Task LoadTabAsync(int tabIndex)
        {
            if (_deliveryNotes == null) return;

            try
            {
                switch (tabIndex)
                {
                    case 1:
                        var prep = await _deliveryNotes.GetPreparedAsync();
                        _rawPrepared = prep
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawPrepared);
                        PreparedItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawPrepared);
                        break;

                    case 2:
                        var del = await _deliveryNotes.GetDeliveredAsync();
                        _rawDelivered = del
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawDelivered);
                        DeliveredItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawDelivered);
                        break;

                    case 3:
                        var pending =
                            await _deliveryNotes.GetPendingQtyJobsAsync();
                        PendingItems =
                            new ObservableCollection<PendingQtyRowViewModel>(
                                pending.Select(x => new PendingQtyRowViewModel(x)));
                        PendingJobsCount = PendingItems.Count;
                        break;

                    case 4:
                        var rework = await _deliveryNotes.GetReworkAsync();
                        _rawRework = rework
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawRework);
                        ReworkItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawRework);
                        ReworkCount = ReworkItems.Count;
                        break;

                    case 5:
                        var returned = await _deliveryNotes.GetReturnedAsync();
                        _rawReturned = returned
                            .Select(x => new DeliveryNoteRowViewModel(x))
                            .ToList();
                        await RefreshSqmCacheAsync(_rawReturned);
                        ReturnedItems =
                            new ObservableCollection<DeliveryNoteRowViewModel>(
                                _rawReturned);
                        ReturnedCount = ReturnedItems.Count;
                        break;

                        // No case 6 — Master DN tab removed
                }
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToReloadTabX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapLoadError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task RefreshSqmCacheAsync(
            List<DeliveryNoteRowViewModel> rows)
        {
            if (_deliveryNotes == null) return;

            var joIds = rows
                .Select(x => x.JobOrderId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (joIds.Count == 0) return;

            var fresh = await _deliveryNotes
                .GetSqmSummariesByJobOrderAsync(joIds);

            foreach (var kvp in fresh)
                _sqmCache[kvp.Key] = kvp.Value;
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTER
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            AllItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawAll));
            PreparedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawPrepared));
            DeliveredItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawDelivered));
            ReworkItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawRework));
            ReturnedItems = new ObservableCollection<DeliveryNoteRowViewModel>(
                Filter(_rawReturned));
        }

        private IEnumerable<DeliveryNoteRowViewModel> Filter(
            IEnumerable<DeliveryNoteRowViewModel> source)
        {
            IEnumerable<DeliveryNoteRowViewModel> query = source;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.DeliveryNoteId ?? "").ToLowerInvariant().Contains(q) ||
                    (x.JobOrderId ?? "").ToLowerInvariant().Contains(q) ||
                    (x.CustomerName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.ProjectName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.DriverName ?? "").ToLowerInvariant().Contains(q));
            }

            if (_selectedStatusFilter != "All Statuses")
                query = query.Where(x => string.Equals(
                    x.DisplayStatus, _selectedStatusFilter,
                    StringComparison.OrdinalIgnoreCase));

            if (_selectedTypeFilter != "All Types")
                query = query.Where(x => string.Equals(
                    x.DeliveryType, _selectedTypeFilter,
                    StringComparison.OrdinalIgnoreCase));

            return query;
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedStatusFilter = "All Statuses";
            _selectedTypeFilter = "All Types";
            SelectedRow = null;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedTypeFilter));

            ApplyFilters();
        }

        // ══════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ══════════════════════════════════════════════════════════════════

        private async Task DeleteAsync(DeliveryNoteRowViewModel row)
        {
            // A-1 (UI layer): tell the user BEFORE the confirm
            // dialog. The repository guard is the real boundary.
            if (!PermissionService.Require(
                    AppPermissions.DeliveryNoteEdit, "delete a delivery note"))
                return;

            // Safety guard — Master DNs must never be deleted from list
            if (row.IsMasterDn)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.MasterDNsCannotBeDeleted"),
                    LocalizationService.Instance.T("Msg.CapCannotDeleteMasterDN"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Phase 14: orphan protection — refuse to delete a DN that has
            // been invoiced.
            var guard = await _database.CheckDependencyAsync(
                DependencyGuard.Kind.DeliveryNote,
                row.DeliveryNoteId);
            if (guard.HasDependents)
            {
                _dialogs.Show(guard.Message, LocalizationService.Instance.T("Msg.CapCannotDelete"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteDeliveryNoteXCustomer2"), row.DeliveryNoteId, row.CustomerName),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (SelectedRow == row) SelectedRow = null;
                await _deliveryNotes!.DeleteAsync(row.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(
            DeliveryNoteRowViewModel row, string newStatus)
        {
            try
            {
                var entity = await _deliveryNotes!.GetByIdAsync(row.Id);
                if (entity == null) return;

                // Phase 3: enforce centralized status-transition rules at the
                // business boundary.
                var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                    .Validate(
                        ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.DeliveryNote,
                        entity.Status, newStatus);
                if (!transition.IsValid)
                {
                    _dialogs.Show(transition.Reason ?? "Invalid status change.",
                        LocalizationService.Instance.T("Msg.CapStatusChangeNotAllowed"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                entity.Status = newStatus;
                entity.UpdatedAt = DateTime.Now;
                await _deliveryNotes.UpdateAsync(entity);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToChangeStatusX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapUpdateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Detaches the static DocumentLifecycle subscriber so this VM is not
        /// held alive (and not re-run) for the whole session after it is no
        /// longer in use. Without this it is a memory leak.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _database.UnsubscribeDocumentsChanged(LoadAsync); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteListViewModel] Dispose detach failed: " + ExceptionMessages.Deep(ex)); }
            GC.SuppressFinalize(this);
        }
    }
}
