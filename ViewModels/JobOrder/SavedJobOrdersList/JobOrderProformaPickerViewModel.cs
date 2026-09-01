// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobOrder/SavedJobOrdersList/JobOrderProformaPickerViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Order — Proforma Invoice picker dialog.
//
// 2026-09-02 (Phase 3.7): the picker's controller logic moved out of
// Views/JobOrder/SavedJobOrdersList/ProformaPicker/JobOrderProformaPickerDialog.xaml.cs
// into this VM, following the repo's VM-dialog pattern: the view wires
// RequestClose to DialogResult and keeps the window chrome + card
// construction; this VM owns loading, filtering, selection, the approval
// warning, and the confirm flow.
//
// Result contract preserved for the caller
// (`_dialogs.Create<JobOrderProformaPickerDialog>()` then
// `dlg.SelectedProformaId` / `dlg.ProceededWithoutApproval`): the window
// forwards those results from this VM.
//
// Deliberately modelled on TaxInvoice.ProformaPickerDialog; two
// Job-Order-specific additions: defaults to APPROVED quotes only (with a
// "show all statuses" toggle, since status is hand-set and can lag a real
// approval), and reads the Step 8 attachment manifest to warn (not block)
// when no approval document is attached.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
    /// <summary>Modal-dialog VM: pick the Proforma Invoice a Job Order
    /// will be built from.</summary>
    public sealed class JobOrderProformaPickerViewModel : ViewModelBase
    {
        public const string ApprovedStatus = "Approved";

        private readonly IDialogService _dialogs;
        private readonly List<PickerItem> _all = new();
        private string _selectedId = "";

        /// <summary>Chosen Proforma id, valid only after RequestClose(true).</summary>
        public string SelectedProformaId { get; private set; } = "";

        /// <summary>True when the operator confirmed despite no approval
        /// document being attached (the caller audits this).</summary>
        public bool ProceededWithoutApproval { get; private set; }

        /// <summary>View wiring point: result=true → DialogResult=true.</summary>
        public Action<bool>? RequestClose { get; set; }

        // ── Bound filter / state ────────────────────────────────────────
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value == _searchText) return;
                _searchText = value;
                OnPropertyChanged();
                Refresh();
            }
        }

        private bool _showAll;
        public bool ShowAll
        {
            get => _showAll;
            set
            {
                if (value == _showAll) return;
                _showAll = value;
                OnPropertyChanged();
                Refresh();
            }
        }

        /// <summary>Live selection id (view calls Select on card click).</summary>
        public string SelectedId => _selectedId;

        public IReadOnlyList<PickerItem> Items { get; private set; } = Array.Empty<PickerItem>();
        public string CountText { get; private set; } = "";
        public string? EmptyMessage { get; private set; }
        public bool EmptyIsError { get; private set; }
        public string SelectedLabelText { get; private set; } = "No selection";
        public bool IsConfirmEnabled { get; private set; }
        public Visibility WarningVisibility { get; private set; } = Visibility.Collapsed;
        public string WarningText { get; private set; } = "";

        public ICommand ConfirmCommand { get; }

        public JobOrderProformaPickerViewModel(IDialogService? dialogs = null)
        {
            _dialogs = dialogs ?? App.ServiceProvider?.GetService<IDialogService>()
                       ?? new DialogService();
            ConfirmCommand = new RelayCommand(_ => Confirm());
        }

        // ── Load ────────────────────────────────────────────────────────

        /// <summary>Called from the view's Loaded. Populates the list and
        /// raises Items/EmptyMessage so the view re-renders.</summary>
        public async Task LoadAsync()
        {
            try
            {
                if (App.Invoices == null)
                {
                    ShowMessage("Proforma Invoice database not initialised.",
                        error: true);
                    return;
                }

                var proformas = await App.Invoices.GetAllAsync();

                if (proformas == null || proformas.Count == 0)
                {
                    ShowMessage(
                        "No Proforma Invoices found.\n\n" +
                        "Create and approve a Proforma Invoice first, then " +
                        "raise the Job Order from it.",
                        error: false);
                    return;
                }

                // One manifest read per quote, up front. The list is small
                // (a picker, not a report) and doing it here keeps the
                // approval badge on every row instead of only the selected
                // one, so the operator can see at a glance which quotes are
                // actually releasable.
                var attachmentService = new ProformaAttachmentService();

                _all.Clear();
                foreach (var p in proformas.OrderByDescending(x => x.CreatedAt))
                {
                    string id = p.ProformaId ?? "";

                    var approval = await DescribeApprovalAsync(
                        attachmentService, id);

                    _all.Add(new PickerItem
                    {
                        ProformaId   = id,
                        CustomerName = p.CustomerName ?? "",
                        ProjectName  = p.ProjectName ?? "",
                        GrandTotal   = p.GrandTotalVatInclusive,
                        Status       = p.Status ?? "",
                        CreatedAt    = p.CreatedAt,
                        ApprovalKind = approval
                    });
                }

                Refresh();
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to load Proforma Invoices:\n{ex.Message}",
                    error: true);
            }
        }

        /// <summary>Returns the approval document type held against a
        /// quote, or empty when none is attached. Recognises the
        /// AttachmentType values the Statement module already writes
        /// ("SignedPI"), plus LPO and manual-release markers, and falls
        /// back to scanning the file name — every attachment uploaded
        /// before this feature existed is typed only "Image" or "Pdf",
        /// so without the fallback every historic quote would look
        /// unapproved.</summary>
        private static async Task<string> DescribeApprovalAsync(
            ProformaAttachmentService service, string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return "";

            try
            {
                List<ProformaAttachmentRecord> manifest =
                    await service.LoadManifestAsync(proformaId);

                if (manifest == null || manifest.Count == 0) return "";

                foreach (var a in manifest)
                {
                    string kind = ClassifyApproval(a);
                    if (!string.IsNullOrEmpty(kind)) return kind;
                }

                return "";
            }
            catch
            {
                // A manifest that will not load must not stop the operator
                // from raising a job order — treat it as "unknown", which
                // shows the same warning as "missing".
                return "";
            }
        }

        private static string ClassifyApproval(ProformaAttachmentRecord a)
        {
            string type = (a.AttachmentType ?? "").Trim();
            string name = (a.FileName ?? "").ToLowerInvariant();
            string desc = (a.Description ?? "").ToLowerInvariant();
            string hay  = name + " " + desc;

            if (type.Equals("SignedPI", StringComparison.OrdinalIgnoreCase))
                return "Signed PI";
            if (type.Equals("LPO", StringComparison.OrdinalIgnoreCase))
                return "LPO";
            if (type.Equals("ManualRelease", StringComparison.OrdinalIgnoreCase))
                return "Manual release";

            if (hay.Contains("lpo") || hay.Contains("purchase order"))
                return "LPO";
            if (hay.Contains("signed") || hay.Contains("approval")
                || hay.Contains("approved"))
                return "Signed PI";
            if (hay.Contains("release"))
                return "Manual release";

            return "";
        }

        // ── Selection / filter / footer ─────────────────────────────────

        public void Select(string proformaId)
        {
            _selectedId = proformaId;
            Refresh();
        }

        private List<PickerItem> CurrentView()
        {
            IEnumerable<PickerItem> q = _all;

            if (!ShowAll)
                q = q.Where(p => p.Status.Equals(
                    ApprovedStatus, StringComparison.OrdinalIgnoreCase));

            string term = (SearchText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
                q = q.Where(p =>
                    p.ProformaId.ToLowerInvariant().Contains(term) ||
                    p.CustomerName.ToLowerInvariant().Contains(term) ||
                    p.ProjectName.ToLowerInvariant().Contains(term));

            return q.ToList();
        }

        /// <summary>Recomputes the filtered view, count line, empty
        /// message and footer; raises everything the view binds to.</summary>
        public void Refresh()
        {
            var items = CurrentView();
            Items = items;
            OnPropertyChanged(nameof(Items));

            int approvedTotal = _all.Count(p => p.Status.Equals(
                ApprovedStatus, StringComparison.OrdinalIgnoreCase));

            CountText = ShowAll
                ? $"Showing {items.Count} of {_all.Count} quotes (all statuses)"
                : $"Showing {items.Count} approved quote(s) — "
                  + $"{_all.Count - approvedTotal} hidden by status filter";
            OnPropertyChanged(nameof(CountText));

            if (items.Count == 0)
            {
                EmptyMessage = ShowAll
                    ? "No quotes match your search."
                    : "No approved Proforma Invoices.\n\n"
                      + "Set a quote's status to Approved on the Proforma "
                      + "list, or tick \"Show all statuses\" to raise a job "
                      + "order from a quote that has not been marked "
                      + "approved yet.";
                EmptyIsError = false;
            }
            else
            {
                EmptyMessage = null;
            }
            OnPropertyChanged(nameof(EmptyMessage));
            OnPropertyChanged(nameof(EmptyIsError));

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            var sel = _all.FirstOrDefault(p => p.ProformaId == _selectedId);

            if (sel == null)
            {
                SelectedLabelText = "No selection";
                IsConfirmEnabled = false;
                WarningVisibility = Visibility.Collapsed;
            }
            else
            {
                SelectedLabelText =
                    $"Selected: {sel.ProformaId} — {sel.CustomerName}";
                IsConfirmEnabled = true;

                bool missing = string.IsNullOrEmpty(sel.ApprovalKind);
                bool notApproved = !sel.Status.Equals(
                    ApprovedStatus, StringComparison.OrdinalIgnoreCase);

                if (!missing && !notApproved)
                {
                    WarningVisibility = Visibility.Collapsed;
                }
                else
                {
                    var parts = new List<string>();
                    if (notApproved)
                        parts.Add($"this quote's status is \"{sel.Status}\", not Approved");
                    if (missing)
                        parts.Add("no signed PI, LPO or release note is attached in Step 8");

                    WarningText =
                        "Check before releasing to production — "
                        + string.Join(", and ", parts)
                        + ". You can still continue; the override is recorded in the "
                        + "audit trail against your user.";
                    WarningVisibility = Visibility.Visible;
                }
            }

            OnPropertyChanged(nameof(SelectedLabelText));
            OnPropertyChanged(nameof(IsConfirmEnabled));
            OnPropertyChanged(nameof(WarningVisibility));
            OnPropertyChanged(nameof(WarningText));
        }

        // ── Confirm ─────────────────────────────────────────────────────

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(_selectedId)) return;

            var sel = _all.FirstOrDefault(p => p.ProformaId == _selectedId);
            if (sel == null) return;

            bool missing = string.IsNullOrEmpty(sel.ApprovalKind);
            bool notApproved = !sel.Status.Equals(
                ApprovedStatus, StringComparison.OrdinalIgnoreCase);

            if (missing || notApproved)
            {
                var confirm = _dialogs.Show(
                    $"{WarningText}\n\n"
                    + $"Create the job order for {sel.ProformaId} anyway?",
                    "Approval Not Confirmed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (confirm != MessageBoxResult.Yes) return;
                ProceededWithoutApproval = true;
            }

            SelectedProformaId = _selectedId;
            RequestClose?.Invoke(true);
        }

        // ── Empty / error state ─────────────────────────────────────────

        private void ShowMessage(string message, bool error)
        {
            EmptyMessage = message;
            EmptyIsError = error;
            OnPropertyChanged(nameof(EmptyMessage));
            OnPropertyChanged(nameof(EmptyIsError));
        }

        /// <summary>One picker row (public so the view can build cards).</summary>
        public sealed class PickerItem
        {
            public string ProformaId { get; init; } = "";
            public string CustomerName { get; init; } = "";
            public string ProjectName { get; init; } = "";
            public decimal GrandTotal { get; init; }
            public string Status { get; init; } = "";
            public DateTime CreatedAt { get; init; }

            /// <summary>"Signed PI" / "LPO" / "Manual release", or empty.</summary>
            public string ApprovalKind { get; init; } = "";
        }
    }
}
