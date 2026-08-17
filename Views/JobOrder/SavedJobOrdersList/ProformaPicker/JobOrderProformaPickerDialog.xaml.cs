using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProGlassAutomation.Models.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;

namespace ProGlassAutomation.Views.JobOrder.SavedJobOrdersList.ProformaPicker
{
    /// <summary>
    /// Picks the Proforma Invoice a Job Order will be built from.
    ///
    /// <para>
    /// Deliberately modelled on the existing
    /// <c>TaxInvoice.TaxInvoiceList.ProformaPickerDialog</c> rather than
    /// inventing a second picker pattern: same window chrome, same
    /// search-and-card list, same confirm/cancel contract. Two things are
    /// added, because a Job Order commits the factory to cutting glass and
    /// a Tax Invoice does not.
    /// </para>
    ///
    /// <list type="number">
    ///   <item>
    ///     Defaults to APPROVED quotes only. The Proforma status vocabulary
    ///     is Draft / Sent / Approved / Rejected / Expired, so "confirmed"
    ///     means Approved. A checkbox re-admits the rest, because the status
    ///     is set by hand and can lag a real customer approval.
    ///   </item>
    ///   <item>
    ///     Reads the Step 8 attachment manifest and reports whether the quote
    ///     carries an approval document — signed PI, LPO, or a manual
    ///     release note. Missing approval WARNS, it does not block: work
    ///     genuinely does start on a verbal go-ahead, and a hard block would
    ///     only teach people to attach a placeholder file.
    ///   </item>
    /// </list>
    /// </summary>
    public partial class JobOrderProformaPickerDialog : Window
    {
        // ── Result ────────────────────────────────────────────────────────

        /// <summary>Chosen Proforma id, valid only when DialogResult is true.</summary>
        public string SelectedProformaId { get; private set; } = "";

        /// <summary>
        /// True when the operator confirmed despite no approval document
        /// being attached. The caller audits this so an unauthorised job can
        /// be traced back to whoever released it.
        /// </summary>
        public bool ProceededWithoutApproval { get; private set; }

        // ── State ─────────────────────────────────────────────────────────

        private readonly List<PickerItem> _all = new();
        private string _selectedId = "";

        private const string ApprovedStatus = "Approved";

        // ── Constructor ───────────────────────────────────────────────────

        public JobOrderProformaPickerDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            Loaded += OnLoaded;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) DialogResult = false;
            };
        }

        // ── Load ──────────────────────────────────────────────────────────

        private async void OnLoaded(object sender, RoutedEventArgs e)
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

                Render();
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to load Proforma Invoices:\n{ex.Message}",
                    error: true);
            }
        }

        /// <summary>
        /// Returns the approval document type held against a quote, or empty
        /// when none is attached.
        /// <para>
        /// Recognises the AttachmentType values the Statement module already
        /// writes ("SignedPI"), plus LPO and manual-release markers, and
        /// falls back to scanning the file name. The fallback matters: every
        /// attachment uploaded before this feature existed is typed only
        /// "Image" or "Pdf", so without it every historic quote would look
        /// unapproved.
        /// </para>
        /// </summary>
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

        // ── Filtering ─────────────────────────────────────────────────────

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
            => Render();

        private void OnShowAllChanged(object sender, RoutedEventArgs e)
            => Render();

        private List<PickerItem> CurrentView()
        {
            IEnumerable<PickerItem> q = _all;

            if (ShowAllCheck?.IsChecked != true)
                q = q.Where(p => p.Status.Equals(
                    ApprovedStatus, StringComparison.OrdinalIgnoreCase));

            string term = (SearchBox?.Text ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
                q = q.Where(p =>
                    p.ProformaId.ToLowerInvariant().Contains(term) ||
                    p.CustomerName.ToLowerInvariant().Contains(term) ||
                    p.ProjectName.ToLowerInvariant().Contains(term));

            return q.ToList();
        }

        // ── Render ────────────────────────────────────────────────────────

        private void Render()
        {
            if (PiList == null) return;

            var items = CurrentView();

            PiList.Children.Clear();

            int approvedTotal = _all.Count(p => p.Status.Equals(
                ApprovedStatus, StringComparison.OrdinalIgnoreCase));

            CountLabel.Text = ShowAllCheck?.IsChecked == true
                ? $"Showing {items.Count} of {_all.Count} quotes (all statuses)"
                : $"Showing {items.Count} approved quote(s) — "
                  + $"{_all.Count - approvedTotal} hidden by status filter";

            if (items.Count == 0)
            {
                ShowMessage(
                    ShowAllCheck?.IsChecked == true
                        ? "No quotes match your search."
                        : "No approved Proforma Invoices.\n\n"
                          + "Set a quote's status to Approved on the Proforma "
                          + "list, or tick \"Show all statuses\" to raise a job "
                          + "order from a quote that has not been marked "
                          + "approved yet.",
                    error: false);
                UpdateFooter();
                return;
            }

            foreach (var item in items)
                PiList.Children.Add(BuildCard(item));

            UpdateFooter();
        }

        private Border BuildCard(PickerItem item)
        {
            bool selected = item.ProformaId == _selectedId;

            var card = new Border
            {
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(14, 12, 14, 12),
                Margin          = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(selected ? 2 : 1.5),
                Cursor          = Cursors.Hand,
                Tag             = item.ProformaId,
                SnapsToDevicePixels = true
            };

            card.SetResourceReference(BackgroundProperty,
                "ModuleBackgroundBrush");
            card.SetResourceReference(BorderBrushProperty,
                selected ? "AccentBrush" : "OutlineBrush");

            card.MouseEnter += (_, _) =>
            {
                if (card.Tag?.ToString() != _selectedId)
                    card.SetResourceReference(
                        BackgroundProperty, "MenuHoverBrush");
            };
            card.MouseLeave += (_, _) =>
            {
                if (card.Tag?.ToString() != _selectedId)
                    card.SetResourceReference(
                        BackgroundProperty, "ModuleBackgroundBrush");
            };
            card.MouseLeftButtonUp += (_, _) => Select(item.ProformaId);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = GridLength.Auto });

            // ── left: id, customer, project, date ──
            var left = new StackPanel();

            var idLine = new TextBlock
            {
                Text = item.ProformaId,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            idLine.SetResourceReference(TextBlock.ForegroundProperty,
                "PrimaryForegroundBrush");

            var meta = new TextBlock
            {
                Text = $"{item.CustomerName}  ·  {item.ProjectName}",
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2)
            };
            meta.SetResourceReference(TextBlock.ForegroundProperty,
                "SecondaryForegroundBrush");

            var date = new TextBlock
            {
                Text = $"{item.CreatedAt:dd MMM yyyy}  ·  {item.Status}",
                FontSize = 10
            };
            date.SetResourceReference(TextBlock.ForegroundProperty,
                "MutedForegroundBrush");

            left.Children.Add(idLine);
            left.Children.Add(meta);
            left.Children.Add(date);
            Grid.SetColumn(left, 0);

            // ── right: amount + approval badge ──
            var right = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var amount = new TextBlock
            {
                Text = $"AED {item.GrandTotal:N2}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 6)
            };
            amount.SetResourceReference(TextBlock.ForegroundProperty,
                "PrimaryForegroundBrush");

            right.Children.Add(amount);
            right.Children.Add(BuildApprovalBadge(item.ApprovalKind));
            Grid.SetColumn(right, 1);

            grid.Children.Add(left);
            grid.Children.Add(right);
            card.Child = grid;
            return card;
        }

        private static Border BuildApprovalBadge(string kind)
        {
            bool has = !string.IsNullOrEmpty(kind);

            var badge = new Border
            {
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(8, 3, 8, 3),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badge.SetResourceReference(BackgroundProperty,
                has ? "TintGreenBrush" : "TintAmberBrush");
            badge.SetResourceReference(BorderBrushProperty,
                has ? "SuccessBrush" : "WarningBrush");

            var row = new StackPanel { Orientation = Orientation.Horizontal };

            var glyph = new TextBlock
            {
                Text = has ? "\uE73E" : "\uE7BA",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty,
                has ? "SuccessBrush" : "WarningBrush");

            var text = new TextBlock
            {
                Text = has ? kind : "No approval",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            text.SetResourceReference(TextBlock.ForegroundProperty,
                "PrimaryForegroundBrush");

            row.Children.Add(glyph);
            row.Children.Add(text);
            badge.Child = row;
            return badge;
        }

        // ── Selection ─────────────────────────────────────────────────────

        private void Select(string proformaId)
        {
            _selectedId = proformaId;
            Render();
        }

        private void UpdateFooter()
        {
            var sel = _all.FirstOrDefault(p => p.ProformaId == _selectedId);

            if (sel == null)
            {
                SelectedLabel.Text = "No selection";
                ConfirmButton.IsEnabled = false;
                ApprovalWarning.Visibility = Visibility.Collapsed;
                return;
            }

            SelectedLabel.Text =
                $"Selected: {sel.ProformaId} — {sel.CustomerName}";
            ConfirmButton.IsEnabled = true;

            bool missing = string.IsNullOrEmpty(sel.ApprovalKind);
            bool notApproved = !sel.Status.Equals(
                ApprovedStatus, StringComparison.OrdinalIgnoreCase);

            if (!missing && !notApproved)
            {
                ApprovalWarning.Visibility = Visibility.Collapsed;
                return;
            }

            var parts = new List<string>();
            if (notApproved)
                parts.Add($"this quote's status is \"{sel.Status}\", not Approved");
            if (missing)
                parts.Add("no signed PI, LPO or release note is attached in Step 8");

            ApprovalWarningText.Text =
                "Check before releasing to production — "
                + string.Join(", and ", parts)
                + ". You can still continue; the override is recorded in the "
                + "audit trail against your user.";
            ApprovalWarning.Visibility = Visibility.Visible;
        }

        // ── Dialog result ─────────────────────────────────────────────────

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedId)) return;

            var sel = _all.FirstOrDefault(p => p.ProformaId == _selectedId);
            if (sel == null) return;

            bool missing = string.IsNullOrEmpty(sel.ApprovalKind);
            bool notApproved = !sel.Status.Equals(
                ApprovedStatus, StringComparison.OrdinalIgnoreCase);

            if (missing || notApproved)
            {
                var confirm = MessageBox.Show(
                    $"{ApprovalWarningText.Text}\n\n"
                    + $"Create the job order for {sel.ProformaId} anyway?",
                    "Approval Not Confirmed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (confirm != MessageBoxResult.Yes) return;
                ProceededWithoutApproval = true;
            }

            SelectedProformaId = _selectedId;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
            => DialogResult = false;

        // ── Empty / error state ───────────────────────────────────────────

        private void ShowMessage(string message, bool error)
        {
            PiList.Children.Clear();

            var text = new TextBlock
            {
                Text = message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(16, 28, 16, 0)
            };

            if (error)
                text.SetResourceReference(TextBlock.ForegroundProperty,
                    "DangerBrush");
            else
                text.SetResourceReference(TextBlock.ForegroundProperty,
                    "SecondaryForegroundBrush");

            PiList.Children.Add(text);
        }

        // ── DTO ───────────────────────────────────────────────────────────

        private sealed class PickerItem
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
