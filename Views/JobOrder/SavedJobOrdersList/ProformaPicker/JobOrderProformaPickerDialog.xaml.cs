using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
    ///
    /// 2026-09-02 (Phase 3.7): loading, filtering, selection, footer and the
    /// confirm flow moved to JobOrderProformaPickerViewModel (WorkflowProgress-
    /// Dialog pattern — RequestClose wired to DialogResult). This file keeps
    /// the window chrome (Escape closes, Owner) and card construction
    /// (BuildCard / BuildApprovalBadge / ShowMessage), re-rendering when the
    /// VM raises Items / EmptyMessage.
    /// </summary>
    public partial class JobOrderProformaPickerDialog : Window
    {
        private readonly JobOrderProformaPickerViewModel _vm = new();

        /// <summary>Chosen Proforma id, valid only when DialogResult is true.</summary>
        public string SelectedProformaId => _vm.SelectedProformaId;

        /// <summary>
        /// True when the operator confirmed despite no approval document
        /// being attached. The caller audits this so an unauthorised job can
        /// be traced back to whoever released it.
        /// </summary>
        public bool ProceededWithoutApproval => _vm.ProceededWithoutApproval;

        public JobOrderProformaPickerDialog()
        {
            InitializeComponent();
            DataContext = _vm;
            Owner = Application.Current.MainWindow;

            _vm.RequestClose = result => DialogResult = result;
            _vm.PropertyChanged += OnVmPropertyChanged;

            Loaded += OnLoaded;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) DialogResult = false;
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
            => await _vm.LoadAsync();

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(JobOrderProformaPickerViewModel.Items) ||
                e.PropertyName == nameof(JobOrderProformaPickerViewModel.EmptyMessage))
                RenderCards();
        }

        // ── Render ────────────────────────────────────────────────────────

        private void RenderCards()
        {
            PiList.Children.Clear();

            if (_vm.EmptyMessage != null)
            {
                ShowMessage(_vm.EmptyMessage, _vm.EmptyIsError);
                return;
            }

            foreach (var item in _vm.Items)
                PiList.Children.Add(BuildCard(item));
        }

        private Border BuildCard(JobOrderProformaPickerViewModel.PickerItem item)
        {
            bool selected = item.ProformaId == _vm.SelectedId;

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
                if (card.Tag?.ToString() != _vm.SelectedId)
                    card.SetResourceReference(
                        BackgroundProperty, "MenuHoverBrush");
            };
            card.MouseLeave += (_, _) =>
            {
                if (card.Tag?.ToString() != _vm.SelectedId)
                    card.SetResourceReference(
                        BackgroundProperty, "ModuleBackgroundBrush");
            };
            card.MouseLeftButtonUp += (_, _) => _vm.Select(item.ProformaId);

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
    }
}
