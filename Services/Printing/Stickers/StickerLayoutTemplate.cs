using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public enum StickerTemplateKind
    {
        Custom,
        Sgu,
        Dgu,
        ProformaInvoice
    }

    public enum StickerTemplateElementKind
    {
        Text,
        Logo,
        Rectangle,
        Line
    }

    public enum StickerTextAlignment
    {
        Left,
        Center,
        Right,
        Justify
    }

    public enum StickerContentAlignment
    {
        Top,
        Center,
        Bottom
    }

    /// <summary>
    /// Portable ProGlass printer layout. Coordinates and dimensions are millimetres
    /// so the saved file is independent of the editor monitor's DPI.
    /// </summary>
    public sealed class StickerLayoutTemplate
    {
        public const string CurrentFileFormat = "ProGlass.StickerTemplate";
        public const int CurrentSchemaVersion = 1;

        public string FileFormat { get; set; } = CurrentFileFormat;
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string TemplateId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Custom sticker";
        public StickerTemplateKind Kind { get; set; } = StickerTemplateKind.Custom;
        public double LabelWidthMm { get; set; } = 100;
        public double LabelHeightMm { get; set; } = 75;
        public double MarginMm { get; set; } = 2.5;
        public double DefaultFontSizePt { get; set; } = 9;
        public string Background { get; set; } = "#FFFFFFFF";
        public ObservableCollection<StickerLayoutElement> Elements { get; set; } = new();

        public StickerLayoutTemplate DeepClone()
        {
            return new StickerLayoutTemplate
            {
                FileFormat = FileFormat,
                SchemaVersion = SchemaVersion,
                TemplateId = TemplateId,
                Name = Name,
                Kind = Kind,
                LabelWidthMm = LabelWidthMm,
                LabelHeightMm = LabelHeightMm,
                MarginMm = MarginMm,
                DefaultFontSizePt = DefaultFontSizePt,
                Background = Background,
                Elements = new ObservableCollection<StickerLayoutElement>(
                    (Elements ?? new ObservableCollection<StickerLayoutElement>())
                    .Where(e => e != null).Select(e => e.DeepClone()))
            };
        }
    }

    /// <summary>A movable, resizable and styleable object on a sticker template.</summary>
    public sealed class StickerLayoutElement : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "Text";
        private StickerTemplateElementKind _kind = StickerTemplateElementKind.Text;
        private string _fieldKey = string.Empty;
        private string _text = "Text";
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private double _xMm = 2;
        private double _yMm = 2;
        private double _widthMm = 45;
        private double _heightMm = 8;
        private string _fontFamily = "Segoe UI";
        private double _fontSizePt = 9;
        private bool _bold;
        private bool _italic;
        private bool _underline;
        private StickerTextAlignment _textAlignment = StickerTextAlignment.Left;
        private StickerContentAlignment _contentAlignment = StickerContentAlignment.Center;
        private string _foreground = "#FF101828";
        private string _background = "#00FFFFFF";
        private string _borderColor = "#00000000";
        private double _borderWidthMm;
        private double _paddingMm;
        private double _rotationDegrees;
        private double _opacity = 1;
        private bool _isVisible = true;
        private bool _hideWhenEmpty = true;
        private bool _isLocked;

        public string Id { get => _id; set => Set(ref _id, value ?? string.Empty); }
        public string Name { get => _name; set => Set(ref _name, value ?? string.Empty); }
        public StickerTemplateElementKind Kind { get => _kind; set => Set(ref _kind, value); }
        /// <summary>Optional source field key; empty means use static Text.</summary>
        public string FieldKey { get => _fieldKey; set => Set(ref _fieldKey, value ?? string.Empty); }
        public string Text { get => _text; set => Set(ref _text, value ?? string.Empty); }
        public string Prefix { get => _prefix; set => Set(ref _prefix, value ?? string.Empty); }
        public string Suffix { get => _suffix; set => Set(ref _suffix, value ?? string.Empty); }
        public double Xmm { get => _xMm; set => Set(ref _xMm, value); }
        public double Ymm { get => _yMm; set => Set(ref _yMm, value); }
        public double WidthMm { get => _widthMm; set => Set(ref _widthMm, value); }
        public double HeightMm { get => _heightMm; set => Set(ref _heightMm, value); }
        public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value ?? "Segoe UI"); }
        public double FontSizePt { get => _fontSizePt; set => Set(ref _fontSizePt, value); }
        public bool Bold { get => _bold; set => Set(ref _bold, value); }
        public bool Italic { get => _italic; set => Set(ref _italic, value); }
        public bool Underline { get => _underline; set => Set(ref _underline, value); }
        public StickerTextAlignment TextAlignment { get => _textAlignment; set => Set(ref _textAlignment, value); }
        public StickerContentAlignment ContentAlignment { get => _contentAlignment; set => Set(ref _contentAlignment, value); }
        public string Foreground { get => _foreground; set => Set(ref _foreground, value ?? "#FF101828"); }
        public string Background { get => _background; set => Set(ref _background, value ?? "#00FFFFFF"); }
        public string BorderColor { get => _borderColor; set => Set(ref _borderColor, value ?? "#00000000"); }
        public double BorderWidthMm { get => _borderWidthMm; set => Set(ref _borderWidthMm, value); }
        public double PaddingMm { get => _paddingMm; set => Set(ref _paddingMm, value); }
        public double RotationDegrees { get => _rotationDegrees; set => Set(ref _rotationDegrees, value); }
        public double Opacity { get => _opacity; set => Set(ref _opacity, value); }
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
        public bool HideWhenEmpty { get => _hideWhenEmpty; set => Set(ref _hideWhenEmpty, value); }
        public bool IsLocked { get => _isLocked; set => Set(ref _isLocked, value); }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? (Kind == StickerTemplateElementKind.Text ? "Text" : Kind.ToString())
            : Name + (string.IsNullOrWhiteSpace(FieldKey) ? string.Empty : " · " + FieldKey);

        public event PropertyChangedEventHandler? PropertyChanged;

        public StickerLayoutElement DeepClone()
        {
            return new StickerLayoutElement
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Name,
                Kind = Kind,
                FieldKey = FieldKey,
                Text = Text,
                Prefix = Prefix,
                Suffix = Suffix,
                Xmm = Xmm,
                Ymm = Ymm,
                WidthMm = WidthMm,
                HeightMm = HeightMm,
                FontFamily = FontFamily,
                FontSizePt = FontSizePt,
                Bold = Bold,
                Italic = Italic,
                Underline = Underline,
                TextAlignment = TextAlignment,
                ContentAlignment = ContentAlignment,
                Foreground = Foreground,
                Background = Background,
                BorderColor = BorderColor,
                BorderWidthMm = BorderWidthMm,
                PaddingMm = PaddingMm,
                RotationDegrees = RotationDegrees,
                Opacity = Opacity,
                IsVisible = IsVisible,
                HideWhenEmpty = HideWhenEmpty,
                IsLocked = IsLocked
            };
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName != nameof(DisplayName)
                && (propertyName == nameof(Name) || propertyName == nameof(FieldKey)))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            return true;
        }
    }

    public sealed class StickerTemplateField
    {
        public StickerTemplateField(string key, string label, bool isImage = false)
        {
            Key = key;
            Label = label;
            IsImage = isImage;
        }

        public string Key { get; }
        public string Label { get; }
        public bool IsImage { get; }
    }

    public sealed class StickerTemplateChoice
    {
        public StickerTemplateChoice(string displayName, StickerLayoutTemplate template,
            bool isBuiltIn, string? filePath = null)
        {
            DisplayName = displayName;
            Template = template;
            IsBuiltIn = isBuiltIn;
            FilePath = filePath ?? string.Empty;
        }

        public string DisplayName { get; }
        public StickerLayoutTemplate Template { get; }
        public bool IsBuiltIn { get; }
        public string FilePath { get; }
    }

    public static class StickerTemplateFieldCatalog
    {
        public static IReadOnlyList<StickerTemplateField> All { get; } = new[]
        {
            new StickerTemplateField(string.Empty, "Static text"),
            new StickerTemplateField("CompanyName", "Company name"),
            new StickerTemplateField("CompanyAddress", "Company address"),
            new StickerTemplateField("CompanyTelephone", "Company telephone"),
            new StickerTemplateField("CompanyMobile", "Representative mobile"),
            new StickerTemplateField("CompanyWebsite", "Company website"),
            new StickerTemplateField("CompanyEmail", "Company email"),
            new StickerTemplateField("OrderDisplay", "Order / Job / PI number"),
            new StickerTemplateField("OrderNumber", "Order number"),
            new StickerTemplateField("JobOrderNumber", "Job Order number"),
            new StickerTemplateField("ProformaNumber", "Proforma number"),
            new StickerTemplateField("CustomerCompanyName", "Customer company"),
            new StickerTemplateField("GlassName", "Glass / colour name"),
            new StickerTemplateField("GlassType", "Glass type (SGU / DGU / LAM)"),
            new StickerTemplateField("PanePosition", "Pane position / side"),
            new StickerTemplateField("WidthMm", "Width in mm"),
            new StickerTemplateField("HeightMm", "Height in mm"),
            new StickerTemplateField("SizeText", "Width × height"),
            new StickerTemplateField("Quantity", "Printed quantity"),
            new StickerTemplateField("Reference", "Project / customer reference"),
            new StickerTemplateField("GlassReference", "Glass reference"),
            new StickerTemplateField("CompanyLogo", "Company logo", isImage: true)
        };

        public static bool Contains(string key) => All.Any(f =>
            string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

        public static StickerTemplateField? Find(string key) => All.FirstOrDefault(f =>
            string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
