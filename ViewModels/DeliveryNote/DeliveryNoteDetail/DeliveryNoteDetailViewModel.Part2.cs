// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteDetailViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Export;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Workflow;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
        public partial class DeliveryNoteDetailViewModel : ViewModelBase
    {
            public DeliveryNoteDetailViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IStatusService>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor (this file)
        // and invoked by the DI-selected parameterized ctor in
        // .Part1.cs, so every construction path initializes fully.
        private void InitializeCore()
        {
            // v2 §24: version history (read-only list for DN — restore
            // needs relational-item support and ships in a later phase).
            ShowVersionsCommand = new RelayCommand(
                async _ => await ShowVersionsAsync());

            // ═══ DETAIL TAB SWITCHER (Overview / Attachments / History) ═══
            SwitchDetailTabCommand = new RelayCommand(p2 =>
                DetailTab = p2 as string ?? "Overview");

            SaveCommand = new RelayCommand(
                async _ =>
                {
                    if (IsOpenForDispatch(Status))
                        await ConfirmDispatchAsync();
                    else
                        await SaveAsync();
                },
                _ => !IsSaving && !IsLoading && !IsMasterDn);

            BackCommand = new RelayCommand(
                _ => OnRequestBackToList?.Invoke());

            MakeDnCommand = new RelayCommand(
                async _ => await ConfirmDispatchAsync(),
                _ => IsOpenForDispatch(Status) && !IsSaving && !IsMasterDn);

            DeleteDnCommand = new RelayCommand(
                async _ => await DeleteDnAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            UploadImageCommand = new RelayCommand(
                async _ => await UploadImageAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            UploadPdfCommand = new RelayCommand(
                async _ => await UploadPdfAsync(),
                _ => _entity != null && !IsSaving && !IsMasterDn);

            OpenAttachmentCommand = new RelayCommand(param =>
            {
                if (param is DnAttachmentRowViewModel row)
                    OpenAttachment(row);
            });

            DeleteAttachmentCommand = new RelayCommand(
                async param =>
                {
                    if (param is DnAttachmentRowViewModel row)
                        await DeleteAttachmentAsync(row);
                },
                canExecute: param =>
                    param is DnAttachmentRowViewModel && !IsMasterDn);

            SelectHistoryEntryCommand = new RelayCommand(async param =>
            {
                if (param is DnHistoryEntryViewModel entry)
                    await NavigateToDnAsync(entry.DeliveryNoteId);
            });

            ExportPdfCommand = new RelayCommand(
                async _ => await GuardedExportAsync(ExportPdfAsync, "PDF"),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportExcelCommand = new RelayCommand(
                async _ => await GuardedExportAsync(ExportExcelAsync, "Excel"),
                _ => _entity != null && !IsSaving && !IsLoading);

            ExportCsvCommand = new RelayCommand(
                async _ => await GuardedExportAsync(ExportCsvAsync, "CSV"),
                _ => _entity != null && !IsSaving && !IsLoading);

            ShareCommand = new RelayCommand(
                async _ => await GuardedExportAsync(ShareAsync, "Share"),
                _ => _entity != null && !IsSaving && !IsLoading);

            // Phase 3: No async void — wrapper uses central AsyncUtility
            ConvertToTaxInvoiceCommand = new RelayCommand(
                execute: param => AsyncUtility.SafeFireAndForget(
                    () => ConvertToTaxInvoiceAsync(param as DnHistoryEntryViewModel),
                    $"DN ConvertToTI {DeliveryNoteId}"),
                canExecute: param => CanConvertToTaxInvoice(
                    param as DnHistoryEntryViewModel));

            // ═══════════════════════════════════════════════════════════
            //  CHAT 19 — FillRemainingCommand
            //  SELECT ALL REMAINING: fills balance qty into DeliveredQty
            //  REMINDER: balance pcs = ALL remaining pcs for that item
            // ═══════════════════════════════════════════════════════════
            FillRemainingCommand = new RelayCommand(
                _ => FillAllRemaining(),
                _ => !IsMasterDn &&
                     !IsSaving &&
                     _itemRows.Any(r => r.BalanceQty > 0));

            // ═══════════════════════════════════════════════════════════
            //  CHAT 19 — ResetQtyCommand
            //  RESET QTY: sets DeliveredQty = 0 for ALL items
            //  Companion to FillRemainingCommand — lets user start over
            // ═══════════════════════════════════════════════════════════
            ResetQtyCommand = new RelayCommand(
                _ => ResetAllQty(),
                _ => !IsMasterDn &&
                     !IsSaving &&
                     _itemRows.Any(r => r.DeliveredQty > 0));

            // CHAT 19 — Create new real DN from Master DN balance
            CreateNewDnFromMasterCommand = new RelayCommand(
                _ =>
                {
                    if (_entity != null)
                        OnRequestCreateDnFromMaster?.Invoke(_entity);
                },
                _ => IsMasterDn &&
                     !IsSaving &&
                     _entity != null &&
                     _itemRows.Any(r => r.BalanceQty > 0));

            ToggleFullMasterCommand = new RelayCommand(
                _ => ShowFullMaster = !ShowFullMaster,
                _ => IsMasterDn && !IsLoading);

            OpenMasterCommand = new RelayCommand(
                _ =>
                {
                    if (!string.IsNullOrWhiteSpace(JobOrderId))
                        OnRequestOpenMasterDn?.Invoke(JobOrderId);
                },
                _ => !IsMasterDn &&
                     !IsLoading &&
                     !string.IsNullOrWhiteSpace(JobOrderId));

            // Sheet Store pattern — clear the selected row so the top strip
            // returns to totals.
            ClearSelectionCommand = new RelayCommand(_ => SelectedItemRow = null);
            ToggleItemSelectionCommand = new RelayCommand(p =>
            {
                if (p is DnItemRowViewModel row)
                    SelectedItemRow = ReferenceEquals(SelectedItemRow, row) ? null : row;
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 19 — FILL ALL REMAINING
        //  REMINDER: balance pcs = ALL remaining pcs for that item.
        //  SR.1 balance=5 → fills 5. SR.2 balance=0 → skipped.
        // ═════════════════════════════════════════════════════════════════

        private void FillAllRemaining()
        {
            // Phase 6: Delegated to domain service
            Services.DeliveryNote.DeliveryNoteLineService.FillAllRemaining(_itemRows);
            RecalculateTotals();
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 19 — RESET ALL QTY
        //  Sets DeliveredQty = 0 for ALL items.
        //  User can start entering quantities fresh.
        // ═════════════════════════════════════════════════════════════════

        private void ResetAllQty()
        {
            // Phase 6: Delegated to domain service
            Services.DeliveryNote.DeliveryNoteLineService.ResetAllQty(_itemRows);
            RecalculateTotals();
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATE TO SIBLING DN
        // ═════════════════════════════════════════════════════════════════

        private async Task NavigateToDnAsync(string targetDeliveryNoteId)
        {
            if (_deliveryNotes == null) return;
            if (_isNavigatingHistory) return;

            if (string.Equals(targetDeliveryNoteId, _deliveryNoteId,
                StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                _isNavigatingHistory = true;

                var target = await _deliveryNotes
                    .GetByDeliveryNoteIdAsync(targetDeliveryNoteId);

                if (target == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.DeliveryNoteXNotFound"), targetDeliveryNoteId),
                        LocalizationService.Instance.T("Msg.CapFound"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await LoadAsync(target.Id);
            }
            finally
            {
                _isNavigatingHistory = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int deliveryNoteId)
        {
            if (_deliveryNotes == null)
            {
                HasError = true;
                ErrorMessage = "Delivery Notes database not initialized.";
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = "";

                _entity = await _deliveryNotes.GetByIdAsync(deliveryNoteId);
                if (_entity == null)
                {
                    HasError = true;
                    ErrorMessage = "Delivery note not found.";
                    return;
                }

                await HydrateFromEntityAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load delivery note: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Rebuilds all UI state from the CURRENT _entity (no DB read).
        /// Shared by LoadAsync and RestoreVersionAsync (v2 §24).
        /// </summary>
        private async Task HydrateFromEntityAsync()
        {
            if (_entity == null) return;

            _suppressDirty = true;
            try
            {
                await HydrateCoreAsync();
            }
            finally
            {
                _suppressDirty = false;
                HasUnsavedChanges = false;   // freshly loaded = clean
            }
        }

        private async Task HydrateCoreAsync()
        {
            _snapshot = DeliveryNoteMapper.DeserializeSnapshot(_entity);

                DeliveryNoteId = _entity.DeliveryNoteId;
                JobOrderId = _entity.JobOrderId;
                ProformaId = _entity.ProformaId;
                DeliveryType = _entity.DeliveryType;
                SequenceNumber = _entity.SequenceNumber;
                DeliveryDateDisplay =
                    _entity.DeliveryDate.ToString("dd MMM yyyy");
                DeliveryTime = _entity.DeliveryTime;

                // Master = flagged, MDN- id, or seq-0 Dummy tracker.
                IsMasterDn = DetectMaster(_entity);
                ShowFullMaster = false;

                var joRef = _snapshot?.JobOrderRef;
                CustomerName = joRef?.CustomerName ?? _entity.CustomerName;
                ProjectName = joRef?.ProjectName ?? _entity.ProjectName;
                ProjectReference = joRef?.ProjectReference ?? "";
                CompanyName = joRef?.CompanyName ?? "";
                CompanyTrn = joRef?.CompanyTrn ?? "";

                Status = _entity.Status;
                IsFoc = _entity.IsFoc;
                DriverName = _entity.DriverName;
                VehicleNumber = _entity.VehicleNumber;
                ReceiverName = _entity.ReceiverName;
                Notes = _entity.Notes;

                // Self-heal the two denormalized cross-DN figures against
                // their authoritative sources BEFORE computing balances:
                //   1. OrderedQty  ← Job Order snapshot (true order qty)
                //   2. PreviouslyDeliveredQty ← confirmed sibling DNs
                // Order matters: Prev heal + balance both read the corrected
                // OrderedQty. Without step 1, a legacy row whose OrderedQty was
                // stored as the balance showed Ordered=64/Prev=0 on the next DN.
                await HealItemOrderedQtyAsync(_entity);

                // Persist sibling totals onto Master, then heal PREV
                // with spec+serial so closed lines drop from the grid.
                await SyncMasterBalancesAsync();
                await HealItemPrevQtyAsync(_entity);
                foreach (var item in _entity.Items)
                {
                    if (IsMasterDn)
                        item.DeliveredQty = 0;
                    item.BalanceQty = Math.Max(0,
                        item.OrderedQty - item.PreviouslyDeliveredQty - item.DeliveredQty);
                }

                // BUG-42: unsubscribe from old rows
                foreach (var oldRow in _itemRows)
                    oldRow.DeliveredQtyChanged -= OnItemDeliveredQtyChanged;

                // DEDUP: one item per (SpecGroupNumber, SerialNumber). The
                // DeliveryNoteItems table has NO unique constraint on
                // (JobOrderId, SpecGroupNumber, SerialNumber), so a stored
                // outer/inner split or a master+real merge can otherwise
                // render one JO row as two (9 rows → 18). Merge duplicates:
                // OrderedQty is REPLICATED across splits → take MAX; delivered
                // quantities accumulate → SUM. We replace _entity.Items with
                // the merged set so display, Save AND Dispatch all operate on
                // the same 1:1 view as the Proforma Invoice Step 5 DataGrid.
                var dedupedItems = _entity.Items
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                    .Select(g =>
                    {
                        var first = g.First();
                        if (g.Count() == 1) return first;
                        return new DeliveryNoteItem
                        {
                            DeliveryNoteId = first.DeliveryNoteId,
                            JobOrderId = first.JobOrderId,
                            SpecGroupNumber = first.SpecGroupNumber,
                            SpecGroupName = first.SpecGroupName,
                            SerialNumber = first.SerialNumber,
                            GlassRef = first.GlassRef,
                            W1 = first.W1,
                            H1 = first.H1,
                            W2 = first.W2,
                            H2 = first.H2,
                            OrderedQty = g.Max(i => i.OrderedQty),
                            PreviouslyDeliveredQty = g.Sum(i => i.PreviouslyDeliveredQty),
                            DeliveredQty = g.Sum(i => i.DeliveredQty),
                            BalanceQty = 0,
                            ReworkQty = g.Sum(i => i.ReworkQty),
                            ReturnedQty = g.Sum(i => i.ReturnedQty),
                            Notes = first.Notes ?? ""
                        };
                    })
                    .OrderBy(i => i.SpecGroupNumber)
                    .ThenBy(i => i.SerialNumber)
                    .ToList();

                _entity.Items = dedupedItems;
                foreach (var item in _entity.Items)
                    item.BalanceQty = item.ComputedBalanceQty;

                // _itemRows holds the COMPLETE deduped line set — including
                // lines already fully delivered on a sibling DN (This=0,
                // Balance=0). Do NOT pre-filter here: the KPI strip needs the
                // whole picture (Ordered / Prev-delivered are JO-level,
                // cross-DN figures), and the "N items fully delivered — hidden"
                // banner counts exactly the lines that pre-filtering used to
                // silently drop. Visibility is decided once, in
                // RebuildGroupedView → ShouldDisplayRow.
                var rows = dedupedItems
                    .Select(i => new DnItemRowViewModel(i))
                    .ToList();

                ItemRows = new ObservableCollection<DnItemRowViewModel>(rows);
                // RebuildGroupedView called by ItemRows setter
                // Filter applied based on IsMasterDn flag already set above

                // A freshly loaded DN has no selected row — the top strip
                // shows totals until the user clicks a row.
                SelectedItemRow = null;

                foreach (var row in ItemRows)
                    row.DeliveredQtyChanged += OnItemDeliveredQtyChanged;

                RecalculateTotals();
                await LoadHistoryAsync();

                if (!_entity.IsMasterDn)
                {
                    var currentInHistory = HistoryEntries
                        .FirstOrDefault(h => string.Equals(
                            h.DeliveryNoteId, DeliveryNoteId,
                            StringComparison.OrdinalIgnoreCase));

                    _selectedHistoryEntry = currentInHistory;
                    if (currentInHistory != null)
                        currentInHistory.IsSelected = true;

                    OnPropertyChanged(nameof(SelectedHistoryEntry));
                    OnPropertyChanged(nameof(IsSelectedDnCurrent));
                    OnPropertyChanged(nameof(SelectedDnHeaderText));
                }

            await LoadAttachmentsDirectlyAsync(DeliveryNoteId);

            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        private async Task HealItemPrevQtyAsync(SavedDeliveryNote entity)
        {
            if (_deliveryNotes == null) return;

            var prevMap = await _deliveryNotes
                .GetPreviouslyDeliveredMapAsync(
                    entity.JobOrderId,
                    entity.DeliveryNoteId);

            foreach (var item in entity.Items)
            {
                item.PreviouslyDeliveredQty = LookupPrev(prevMap, item);
            }
        }

        /// <summary>
        /// Re-derive OrderedQty for every line straight from the authoritative
        /// Job Order snapshot — the ERP source of truth. OrderedQty is a
        /// denormalized value copied master → DN → DN; if a historical build
        /// ever stored the *balance* into OrderedQty, that wrong number
        /// propagates forever because nothing recomputes it. This is the
        /// missing counterpart to HealItemPrevQtyAsync: together they make the
        /// KPI strip (Ordered / Prev / This DN / Balance) self-healing and
        /// correct even on rows saved by older, buggy builds.
        ///
        /// Keys are (SpecGroupNumber | continuous SerialNumber), matching how
        /// DeliveryNoteMapper.FromJobOrder assigns line identity, so the map
        /// aligns 1:1 with the stored DeliveryNoteItem rows. If the JO can't be
        /// read (deleted, or a legacy Dummy with no JO), stored values are left
        /// untouched — never zero a line just because the source is missing.
        /// </summary>
        private async Task HealItemOrderedQtyAsync(SavedDeliveryNote entity)
        {
            try
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.JobOrderId))
                    return;

                var jobOrder = await ProGlassAutomation.App.Jobs
                    .GetByJobOrderIdAsync(entity.JobOrderId);
                if (jobOrder == null) return;

                var joSnapshot = ProGlassAutomation.Services.JobOrder.Mapper
                    .JobOrderMapper.DeserializeSnapshot(jobOrder);
                if (joSnapshot == null) return;

                var orderedMap = DeliveryNoteMapper.BuildOrderedQtyMap(joSnapshot);
                if (orderedMap.Count == 0) return;

                foreach (var item in entity.Items)
                {
                    var key = DeliveryNoteWorkflow.LineKey(
                        item.SpecGroupNumber, item.SerialNumber);
                    if (orderedMap.TryGetValue(key, out var trueOrdered)
                        && trueOrdered > 0
                        && trueOrdered != item.OrderedQty)
                    {
                        item.OrderedQty = trueOrdered;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[DN] HealItemOrderedQty skipped: "
                    + ExceptionMessages.Deep(ex));
            }
        }

        private static int LookupPrev(
            Dictionary<string, int> map, DeliveryNoteItem item)
        {
            if (map.TryGetValue(
                    DeliveryNoteWorkflow.LineKey(item.SpecGroupNumber, item.SerialNumber),
                    out var bySpec) && bySpec > 0)
                return bySpec;
            return 0;
        }

        /// <summary>
        /// Writes sibling totals onto the Master row so the DB matches
        /// the grid. Saved Drafts count — they are already in the list.
        /// </summary>
        private async Task SyncMasterBalancesAsync()
        {
            if (_entity == null || !DetectMaster(_entity)) return;
            if (_deliveryNotes == null) return;
            if (!PermissionService.Can(AppPermissions.DeliveryNoteEdit))
                return;

            try
            {
                await _deliveryNotes.UpdateMasterDnAsync(
                    _entity.JobOrderId, _entity.DeliveryNoteId);

                var fresh = await _deliveryNotes
                    .GetMasterDnForJobOrderAsync(_entity.JobOrderId);
                if (fresh == null) return;

                _entity.Items = fresh.Items;
                _entity.TotalOrderedQty = fresh.TotalOrderedQty;
                _entity.TotalDeliveredQty = fresh.TotalDeliveredQty;
                _entity.TotalBalanceQty = fresh.TotalBalanceQty;
                _entity.TotalPreviouslyDeliveredQty =
                    fresh.TotalPreviouslyDeliveredQty;
                _entity.LastUpdatedFromDnId = fresh.LastUpdatedFromDnId;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DN] SyncMasterBalances skipped: {ExceptionMessages.Deep(ex)}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD HISTORY
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadHistoryAsync()
        {
            if (_deliveryNotes == null ||
                string.IsNullOrWhiteSpace(JobOrderId)) return;

            var history = await _deliveryNotes
                .GetDeliveryHistoryAsync(JobOrderId);

            HistoryTotalOrdered = history.TotalOrdered;
            HistoryTotalDelivered = history.TotalDelivered;
            HistoryTotalBalance = history.TotalBalance;

            var siblingIds = history.Entries
                .Select(e => e.DeliveryNoteId).ToList();
            var counts = await _deliveryNotes
                .GetAttachmentCountsAsync(siblingIds);

            var entries = history.Entries.Select(e =>
            {
                var vm = new DnHistoryEntryViewModel(e);
                vm.AttachmentCount = counts.TryGetValue(
                    e.DeliveryNoteId, out var c) ? c : 0;
                vm.IsCurrentDn = string.Equals(
                    e.DeliveryNoteId, DeliveryNoteId,
                    StringComparison.OrdinalIgnoreCase);
                if (vm.IsCurrentDn) vm.IsSelected = true;
                return vm;
            })
            .OrderByDescending(vm => vm.SequenceNumber)
            .ToList();

            HistoryEntries =
                new ObservableCollection<DnHistoryEntryViewModel>(entries);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE (disabled for Master DN)
        // ═════════════════════════════════════════════════════════════════

        public async Task SaveAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.DeliveryNoteEdit, "save a delivery note"))
                return;

            if (_entity == null || _deliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

            // ERP guard: run the centralized validation (delivered ≤ remaining,
            // non-negative) before touching the DB. This service was registered
            // and unit-tested but never actually invoked on the Save path.
            var saveCheck = DeliveryNoteValidationService.ValidateSave(_entity, ItemRows);
            if (saveCheck.IsFailure)
            {
                _dialogs.Show(
                    string.IsNullOrWhiteSpace(saveCheck.Error) ? "Delivery note failed validation." : saveCheck.Error,
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsSaving = true;

                _entity.Status = Status;
                _entity.IsFoc = IsFoc;
                _entity.DriverName = DriverName;
                _entity.VehicleNumber = VehicleNumber;
                _entity.ReceiverName = ReceiverName;
                _entity.Notes = Notes;

                SyncRowsToEntity();

                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);

                await _deliveryNotes.UpdateAsync(_entity);

                try
                {
                    await _deliveryNotes.UpdateMasterDnAsync(
                        _entity.JobOrderId, _entity.DeliveryNoteId);
                }
                catch (Exception masterEx)
                {
                    AppLogService.Debug(
                        $"[DN] Master update after save skipped: {masterEx.Message}");
                }

                HasUnsavedChanges = false;   // v2 §28: clean after save

                // v2 §24: append a restorable version of every save.
                // (Restore for DN needs relational-item support — the
                // snapshots are preserved now so that can ship later
                // without losing any history.)
                _ = ProGlassAutomation.Services.Audit.VersionHistoryService
                    .AddVersionAsync(
                        "DN", _entity.DeliveryNoteId,
                        $"Status {Status}",
                        _entity.FullSnapshotJson);

                await LoadAsync(_entity.Id);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery note saved",
                    $"{DeliveryNoteId} — status {Status}",
                    "DN", DeliveryNoteId);

            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToSaveX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MAKE DN — CHAT 19 FIXED
        //  Calls UpdateMasterDnAsync instead of creating new Dummy DN
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// v2 §24: shows the version history for the delivery note being
        /// edited and applies a chosen restore as a PENDING change.
        /// </summary>
        private async Task ShowVersionsAsync()
        {
            if (string.IsNullOrWhiteSpace(DeliveryNoteId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.SaveTheDeliveryNoteFirst"),
                    LocalizationService.Instance.T("Common.VersionHistory"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var versions = await ProGlassAutomation.Services.Audit
                .VersionHistoryService
                .GetVersionsAsync("DN", DeliveryNoteId);

            var restoreId = Views.ProformaInvoice.SavedInvoicesList
                .VersionsDialog.Show(
                    Application.Current?.MainWindow,
                    DeliveryNoteId, versions);

            if (restoreId == null) return;

            bool ok = await RestoreVersionAsync(restoreId.Value);
            if (!ok)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.VersionCouldNotBeApplied"),
                    LocalizationService.Instance.T("Msg.CapRestoreVersion"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// v2 §24: applies an old version to the CURRENT entity in memory.
        /// Header fields are restored fully; line items are restored
        /// quantity-by-quantity (matched by spec group + serial) so the
        /// relational structure is never broken. Nothing is written to
        /// the DB until the user saves — saving appends a NEW version.
        /// </summary>
        public async Task<bool> RestoreVersionAsync(int versionId)
        {
            try
            {
                if (_entity == null || _entity.IsMasterDn) return false;

                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null ||
                    string.IsNullOrEmpty(version.SnapshotJson))
                    return false;

                // Deserialize the old snapshot via a throwaway shell.
                var shell = new SavedDeliveryNote
                {
                    FullSnapshotJson = version.SnapshotJson
                };
                var snap = DeliveryNoteMapper.DeserializeSnapshot(shell);
                if (snap == null) return false;

                // Header fields
                _entity.Status = snap.Header.Status;
                _entity.IsFoc = snap.Header.IsFoc;
                _entity.DriverName = snap.Header.DriverName;
                _entity.VehicleNumber = snap.Header.VehicleNumber;
                _entity.ReceiverName = snap.Header.ReceiverName;
                _entity.Notes = snap.Header.Notes;
                _entity.DeliveryTime = snap.Header.DeliveryTime;

                // Item quantities (structure preserved, quantities restored)
                foreach (var sItem in snap.Items)
                {
                    var item = _entity.Items.FirstOrDefault(i =>
                        i.SpecGroupNumber == sItem.SpecGroupNumber &&
                        i.SerialNumber == sItem.SerialNumber);
                    if (item == null) continue;

                    item.DeliveredQty = sItem.DeliveredQty;
                    item.BalanceQty = Math.Max(0,
                        item.OrderedQty
                        - item.PreviouslyDeliveredQty
                        - item.DeliveredQty);
                    item.Notes = sItem.Notes ?? "";
                }

                // Rebuild the whole UI from the updated entity (no DB read)
                await HydrateFromEntityAsync();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.DeliveryNote,
                    "Version restored",
                    $"{_entity.DeliveryNoteId} → version {version.VersionNo} " +
                    "(pending save)",
                    "DN", _entity.DeliveryNoteId);

                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DN] RestoreVersion failed: {ExceptionMessages.Deep(ex)}");
                return false;
            }
        }

        private async Task ConfirmDispatchAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.DeliveryNoteEdit, "dispatch a delivery note"))
                return;

            if (_entity == null || _deliveryNotes == null) return;
            if (_entity.IsMasterDn) return;

            // ERP guards, centralized: (1) qty sanity (delivered ≤ remaining),
            // (2) at least one delivered pc, (3) the Dispatching→Dispatched
            // transition is legal. These previously lived only as ad-hoc
            // inline checks; route them through the shared validation service.
            var qtyCheck = DeliveryNoteValidationService.ValidateQuantities(ItemRows);
            if (qtyCheck.IsFailure)
            {
                _dialogs.Show(
                    string.IsNullOrWhiteSpace(qtyCheck.Error) ? "Delivery quantities are invalid." : qtyCheck.Error,
                    LocalizationService.Instance.T("Msg.CapDispatchFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dispatchCheck = DeliveryNoteValidationService.ValidateDispatch(ItemRows, _entity.IsMasterDn);
            if (dispatchCheck.IsFailure)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotDispatchNoItemsHave"),
                    LocalizationService.Instance.T("Msg.CapDispatchFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var transitionCheck = DeliveryNoteValidationService.ValidateStatusTransition(
                Status, DeliveryNoteStatusValues.Dispatched);
            if (transitionCheck.IsFailure)
            {
                _dialogs.Show(
                    string.IsNullOrWhiteSpace(transitionCheck.Error) ? "This delivery note cannot be dispatched from its current status." : transitionCheck.Error,
                    LocalizationService.Instance.T("Msg.CapDispatchFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsSaving = true;
                Status = DeliveryNoteStatusValues.Dispatched;

                _entity.Status = Status;
                _entity.IsFoc = IsFoc;
                _entity.DriverName = DriverName;
                _entity.VehicleNumber = VehicleNumber;
                _entity.ReceiverName = ReceiverName;
                _entity.Notes = Notes;

                SyncRowsToEntity();

                var joRef = _snapshot?.JobOrderRef;
                DeliveryNoteMapper.RefreshSnapshot(_entity, joRef);
                await _deliveryNotes.UpdateAsync(_entity);

                // CHAT 19: update Master DN — no new Dummy DN
                await _deliveryNotes.UpdateMasterDnAsync(
                    _entity.JobOrderId,
                    _entity.DeliveryNoteId);

                AuditService.Log(
                    AuditSections.DeliveryNote, "Delivery dispatched",
                    $"{_entity.DeliveryNoteId} dispatched",
                    "DN", _entity.DeliveryNoteId);

                HasUnsavedChanges = false;

                int deliveredThisDn = _entity.Items.Sum(i => i.DeliveredQty);
                string invoiceNote = "";
                if (deliveredThisDn > 0 && !_entity.HasTaxInvoice
                    && OnRequestAutoCreateTaxInvoice != null)
                {
                    try
                    {
                        string invoiceNo = await OnRequestAutoCreateTaxInvoice(
                            _entity.DeliveryNoteId);
                        if (!string.IsNullOrWhiteSpace(invoiceNo))
                            invoiceNote =
                                $"Tax Invoice {invoiceNo} created as Draft " +
                                $"for this delivery ({deliveredThisDn} pcs).\n" +
                                "Open Tax Invoice to edit dates, bank or rates.";
                    }
                    catch (Exception tiEx)
                    {
                        invoiceNote =
                            "Delivery dispatched, but the tax invoice could not " +
                            $"be created automatically:\n{tiEx.Message}\n" +
                            "Use Convert to Tax Invoice from History.";
                    }
                }

                if (!string.IsNullOrWhiteSpace(invoiceNote))
                {
                    _dialogs.Show(
                        invoiceNote,
                        LocalizationService.Instance.T("Msg.CapDispatched"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                OnRequestBackToList?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DispatchFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapDispatchFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
