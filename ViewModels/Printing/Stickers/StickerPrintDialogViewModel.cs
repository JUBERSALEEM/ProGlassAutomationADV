using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.ViewModels.Printing.Stickers
{
    public sealed class StickerPrintDialogViewModel : ViewModelBase
    {
        public const long MaximumBatchSize = 50_000;

        private string _widthMmText;
        private string _heightMmText;
        private string _marginMmText;
        private string _fontSizePtText;
        private bool _oneLabelPerPiece;
        private bool _showLogo;
        private bool _showCompanyName;
        private bool _showAddress;
        private bool _showTelephone;
        private bool _showMobile;
        private bool _showWebsite;
        private bool _showEmail;
        private bool _showOrder;
        private bool _showCustomer;
        private bool _showGlassName;
        private bool _showSize;
        private bool _showQuantity;
        private bool _showReference;
        private bool _showPanePosition;
        private GlassStickerLabel? _selectedLabel;
        private string _statusMessage = string.Empty;

        public StickerPrintDialogViewModel(
            System.Collections.Generic.IEnumerable<GlassStickerLabel> labels,
            StickerPrintPreferences? preferences)
        {
            var settings = preferences?.Copy() ?? new StickerPrintPreferences();
            Items = new ObservableCollection<GlassStickerLabel>(labels ?? Array.Empty<GlassStickerLabel>());
            _selectedLabel = Items.FirstOrDefault();
            _widthMmText = Format(settings.LabelWidthMm);
            _heightMmText = Format(settings.LabelHeightMm);
            _marginMmText = Format(settings.MarginMm);
            _fontSizePtText = Format(settings.FontSizePt);
            _oneLabelPerPiece = settings.OneLabelPerPiece;
            _showLogo = settings.ShowLogo;
            _showCompanyName = settings.ShowCompanyName;
            _showAddress = settings.ShowAddress;
            _showTelephone = settings.ShowTelephone;
            _showMobile = settings.ShowMobile;
            _showWebsite = settings.ShowWebsite;
            _showEmail = settings.ShowEmail;
            _showOrder = settings.ShowOrder;
            _showCustomer = settings.ShowCustomer;
            _showGlassName = settings.ShowGlassName;
            _showSize = settings.ShowSize;
            _showQuantity = settings.ShowQuantity;
            _showReference = settings.ShowReference;
            _showPanePosition = settings.ShowPanePosition;
        }

        public ObservableCollection<GlassStickerLabel> Items { get; }

        public GlassStickerLabel? SelectedLabel
        {
            get => _selectedLabel;
            set => Set(ref _selectedLabel, value, notifyBatch: false);
        }

        public string WidthMmText { get => _widthMmText; set => Set(ref _widthMmText, value ?? string.Empty); }
        public string HeightMmText { get => _heightMmText; set => Set(ref _heightMmText, value ?? string.Empty); }
        public string MarginMmText { get => _marginMmText; set => Set(ref _marginMmText, value ?? string.Empty); }
        public string FontSizePtText { get => _fontSizePtText; set => Set(ref _fontSizePtText, value ?? string.Empty); }
        public bool OneLabelPerPiece { get => _oneLabelPerPiece; set => Set(ref _oneLabelPerPiece, value); }
        public bool ShowLogo { get => _showLogo; set => Set(ref _showLogo, value); }
        public bool ShowCompanyName { get => _showCompanyName; set => Set(ref _showCompanyName, value); }
        public bool ShowAddress { get => _showAddress; set => Set(ref _showAddress, value); }
        public bool ShowTelephone { get => _showTelephone; set => Set(ref _showTelephone, value); }
        public bool ShowMobile { get => _showMobile; set => Set(ref _showMobile, value); }
        public bool ShowWebsite { get => _showWebsite; set => Set(ref _showWebsite, value); }
        public bool ShowEmail { get => _showEmail; set => Set(ref _showEmail, value); }
        public bool ShowOrder { get => _showOrder; set => Set(ref _showOrder, value); }
        public bool ShowCustomer { get => _showCustomer; set => Set(ref _showCustomer, value); }
        public bool ShowGlassName { get => _showGlassName; set => Set(ref _showGlassName, value); }
        public bool ShowSize { get => _showSize; set => Set(ref _showSize, value); }
        public bool ShowQuantity { get => _showQuantity; set => Set(ref _showQuantity, value); }
        public bool ShowReference { get => _showReference; set => Set(ref _showReference, value); }
        public bool ShowPanePosition { get => _showPanePosition; set => Set(ref _showPanePosition, value); }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value ?? string.Empty, notifyBatch: false);
        }

        public string EstimatedLabelCountText
        {
            get
            {
                long count = EstimatedLabelCount;
                return count > MaximumBatchSize
                    ? "More than 50,000 labels — split the batch"
                    : count.ToString("N0", CultureInfo.CurrentCulture) + " label(s)";
            }
        }

        public long EstimatedLabelCount
        {
            get
            {
                long count = 0;
                foreach (var row in Items)
                {
                    if (row == null || row.WidthMm <= 0 || row.HeightMm <= 0 || row.Quantity <= 0)
                        continue;
                    count += OneLabelPerPiece ? row.Quantity : 1;
                    if (count > MaximumBatchSize) return count;
                }
                return count;
            }
        }

        public bool TryCreatePreferences(out StickerPrintPreferences preferences, out string error)
        {
            preferences = new StickerPrintPreferences();
            error = string.Empty;

            if (!TryParseNumber(WidthMmText, out double width)
                || width < 15 || width > 500)
            {
                error = "Label width must be between 15 and 500 mm.";
                return false;
            }
            if (!TryParseNumber(HeightMmText, out double height)
                || height < 15 || height > 500)
            {
                error = "Label height must be between 15 and 500 mm.";
                return false;
            }
            if (!TryParseNumber(MarginMmText, out double margin)
                || margin < 0 || margin > 25 || margin * 2 >= Math.Min(width, height))
            {
                error = "Margin must be between 0 and 25 mm and leave printable space.";
                return false;
            }
            if (!TryParseNumber(FontSizePtText, out double fontSize)
                || fontSize < 5 || fontSize > 30)
            {
                error = "Font size must be between 5 and 30 pt.";
                return false;
            }
            if (Items.Count == 0 || EstimatedLabelCount == 0)
            {
                error = "There are no valid glass panes to print.";
                return false;
            }
            if (EstimatedLabelCount > MaximumBatchSize)
            {
                error = "This batch exceeds 50,000 labels. Filter or split the source job before printing.";
                return false;
            }

            preferences = new StickerPrintPreferences
            {
                LabelWidthMm = width,
                LabelHeightMm = height,
                MarginMm = margin,
                FontSizePt = fontSize,
                OneLabelPerPiece = OneLabelPerPiece,
                ShowLogo = ShowLogo,
                ShowCompanyName = ShowCompanyName,
                ShowAddress = ShowAddress,
                ShowTelephone = ShowTelephone,
                ShowMobile = ShowMobile,
                ShowWebsite = ShowWebsite,
                ShowEmail = ShowEmail,
                ShowOrder = ShowOrder,
                ShowCustomer = ShowCustomer,
                ShowGlassName = ShowGlassName,
                ShowSize = ShowSize,
                ShowQuantity = ShowQuantity,
                ShowReference = ShowReference,
                ShowPanePosition = ShowPanePosition
            };
            return true;
        }

        private static bool TryParseNumber(string text, out double value)
        {
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return parsed && double.IsFinite(value);
        }

        private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        private bool Set<T>(ref T field, T value, bool notifyBatch = true,
            [CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            if (notifyBatch)
            {
                OnPropertyChanged(nameof(EstimatedLabelCount));
                OnPropertyChanged(nameof(EstimatedLabelCountText));
            }
            return true;
        }
    }
}
