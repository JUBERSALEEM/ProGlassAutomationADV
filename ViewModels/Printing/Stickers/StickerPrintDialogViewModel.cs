using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.ViewModels.Printing.Stickers
{
    public class StickerPrintDialogViewModel : ViewModelBase
    {
        public const long MaximumBatchSize = StickerPrintEngine.MaximumBatchSize;

        private string _widthMmText;
        private string _heightMmText;
        private string _marginMmText;
        private string _sheetMarginMmText;
        private string _sheetGapMmText;
        private string _fontSizePtText;
        private StickerPrinterModel _printerModel;
        private StickerOutputLayoutMode _outputLayoutMode;
        private StickerPrinterResolution _printerResolution;
        private StickerMediaHandling _mediaHandling;
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
        private GlassStickerLabel? _previewLabel;
        private int _selectedSourceRowIndex = -1;
        private StickerLayoutTemplate _activeTemplate = new();
        private StickerTemplateChoice? _selectedTemplateChoice;
        private StickerLayoutElement? _selectedElement;
        private string _newFieldKey = "CompanyName";
        private bool _isTemplateDirty;
        private string _statusMessage = string.Empty;
        private bool _loadingTemplate;
        private int _selectedStudioTabIndex;

        private readonly StickerPrinterOption<StickerPrinterModel>[] _printerModelOptions =
        {
            new(StickerPrinterModel.Custom, "Custom / other printer"),
            new(StickerPrinterModel.ZebraZt411, "Zebra ZT411")
        };
        private readonly StickerPrinterOption<StickerPrinterResolution>[] _printerResolutionOptions =
        {
            new(StickerPrinterResolution.Dpi203, "203 dpi"),
            new(StickerPrinterResolution.Dpi300, "300 dpi"),
            new(StickerPrinterResolution.Dpi600, "600 dpi")
        };
        private readonly StickerPrinterOption<StickerMediaHandling>[] _mediaHandlingOptions =
        {
            new(StickerMediaHandling.TearOff, "Tear-off"),
            new(StickerMediaHandling.PeelOff, "Peel-off"),
            new(StickerMediaHandling.Rewind, "Rewind"),
            new(StickerMediaHandling.Cutter, "Cutter + catch tray")
        };
        private readonly StickerPrinterOption<StickerOutputLayoutMode>[] _standardPrinterLayoutOptions =
        {
            new(StickerOutputLayoutMode.PrinterDefault, "Automatic · A4 sheet for standard printers"),
            new(StickerOutputLayoutMode.A4Sheet, "A4 sheet · arrange multiple stickers per page"),
            new(StickerOutputLayoutMode.OneLabelPerPage, "One sticker per page · roll / custom stock")
        };
        private readonly StickerPrinterOption<StickerOutputLayoutMode>[] _zt411LayoutOptions =
        {
            new(StickerOutputLayoutMode.PrinterDefault, "Automatic · one custom-size label per page"),
            new(StickerOutputLayoutMode.OneLabelPerPage, "One sticker per page · roll / custom stock")
        };

        public StickerPrintDialogViewModel(
            System.Collections.Generic.IEnumerable<GlassStickerLabel> labels,
            StickerPrintPreferences? preferences,
            GlassStickerLabel? previewLabel = null)
        {
            var settings = preferences?.Copy() ?? new StickerPrintPreferences();
            bool hasLegacySettings = settings.LastTemplate == null
                && (settings.HasPersistedSettings || !string.IsNullOrWhiteSpace(settings.LastTemplateName));
            Items = new ObservableCollection<GlassStickerLabel>(labels ?? Array.Empty<GlassStickerLabel>());

            TemplateChoices = new ObservableCollection<StickerTemplateChoice>();
            foreach (var preset in StickerTemplatePresets.All)
                TemplateChoices.Add(new StickerTemplateChoice(preset.Name, preset, isBuiltIn: true));
            foreach (var stored in StickerTemplateStore.LoadLibrary())
                TemplateChoices.Add(new StickerTemplateChoice(
                    stored.Template.Name + " · saved", stored.Template, isBuiltIn: false, filePath: stored.Path));

            if (settings.LastTemplate != null
                && StickerTemplateStore.TryValidate(settings.LastTemplate, out _))
            {
                var lastChoice = new StickerTemplateChoice(
                    settings.LastTemplate.Name + " · last used", settings.LastTemplate, isBuiltIn: false);
                TemplateChoices.Insert(0, lastChoice);
                _selectedTemplateChoice = lastChoice;
                _activeTemplate = settings.LastTemplate.DeepClone();
            }
            else
            {
                var selected = TemplateChoices.FirstOrDefault(choice =>
                    string.Equals(choice.Template.Name, settings.LastTemplateName,
                        StringComparison.OrdinalIgnoreCase))
                    ?? TemplateChoices.FirstOrDefault(choice => choice.Template.Kind == StickerTemplateKind.Sgu)
                    ?? TemplateChoices.First();
                _selectedTemplateChoice = selected;
                _activeTemplate = selected.Template.DeepClone();

                // Preferences created by the first-generation sticker dialog
                // remain useful when an operator upgrades to the template editor.
                // A fresh install keeps the dimensions chosen by the selected preset.
                if (hasLegacySettings)
                {
                    _activeTemplate.LabelWidthMm = settings.LabelWidthMm;
                    _activeTemplate.LabelHeightMm = settings.LabelHeightMm;
                    _activeTemplate.MarginMm = settings.MarginMm;
                    _activeTemplate.DefaultFontSizePt = settings.FontSizePt;
                    ApplyLegacyFieldVisibility(settings);
                }
            }

            _previewLabel = Items.Count == 0
                ? previewLabel ?? StickerTemplateSampleFactory.Create(_activeTemplate.Kind)
                : null;
            _selectedLabel = Items.FirstOrDefault() ?? _previewLabel;
            _selectedSourceRowIndex = Items.Count == 0 ? -1 : 0;

            _widthMmText = Format(_activeTemplate.LabelWidthMm);
            _heightMmText = Format(_activeTemplate.LabelHeightMm);
            _marginMmText = Format(_activeTemplate.MarginMm);
            _sheetMarginMmText = Format(settings.SheetMarginMm);
            _sheetGapMmText = Format(settings.SheetGapMm);
            _fontSizePtText = Format(_activeTemplate.DefaultFontSizePt);
            _printerModel = Enum.IsDefined(typeof(StickerPrinterModel), settings.PrinterModel)
                ? settings.PrinterModel : StickerPrinterModel.Custom;
            _outputLayoutMode = Enum.IsDefined(typeof(StickerOutputLayoutMode), settings.OutputLayoutMode)
                ? settings.OutputLayoutMode : StickerOutputLayoutMode.PrinterDefault;
            if (_printerModel == StickerPrinterModel.ZebraZt411
                && _outputLayoutMode == StickerOutputLayoutMode.A4Sheet)
                _outputLayoutMode = StickerOutputLayoutMode.PrinterDefault;
            _printerResolution = settings.PrinterResolution;
            _mediaHandling = settings.MediaHandling;
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
            _templateKindOptions = new[]
            {
                new StickerTemplateKindOption(StickerTemplateKind.Sgu, "SGU · Single glass"),
                new StickerTemplateKindOption(StickerTemplateKind.Dgu, "DGU · Insulated glass"),
                new StickerTemplateKindOption(StickerTemplateKind.ProformaInvoice, "Proforma / Job"),
                new StickerTemplateKindOption(StickerTemplateKind.Custom, "Custom layout")
            };
            _textAlignments = Enum.GetValues<StickerTextAlignment>().ToArray();
            _contentAlignments = Enum.GetValues<StickerContentAlignment>().ToArray();
            _fieldChoices = StickerTemplateFieldCatalog.All;
            AttachTemplateEvents();
        }

        private readonly StickerTemplateKindOption[] _templateKindOptions;
        private readonly StickerTextAlignment[] _textAlignments;
        private readonly StickerContentAlignment[] _contentAlignments;
        private readonly System.Collections.Generic.IReadOnlyList<StickerTemplateField> _fieldChoices;

        public ObservableCollection<GlassStickerLabel> Items { get; }
        public ObservableCollection<StickerTemplateChoice> TemplateChoices { get; }
        public StickerTemplateKindOption[] TemplateKindOptions => _templateKindOptions;
        public StickerPrinterOption<StickerPrinterModel>[] PrinterModelOptions => _printerModelOptions;
        public StickerPrinterOption<StickerPrinterResolution>[] PrinterResolutionOptions => _printerResolutionOptions;
        public StickerPrinterOption<StickerMediaHandling>[] MediaHandlingOptions => _mediaHandlingOptions;
        public StickerPrinterOption<StickerOutputLayoutMode>[] OutputLayoutOptions => IsZt411Profile
            ? _zt411LayoutOptions : _standardPrinterLayoutOptions;
        public StickerTextAlignment[] TextAlignments => _textAlignments;
        public StickerContentAlignment[] ContentAlignments => _contentAlignments;
        public System.Collections.Generic.IReadOnlyList<StickerTemplateField> FieldChoices => _fieldChoices;
        public ObservableCollection<StickerLayoutElement> TemplateElements => _activeTemplate.Elements;
        public StickerLayoutTemplate ActiveTemplate => _activeTemplate;
        public bool IsSamplePreview => Items.Count == 0;
        public bool CanNavigateSourceRows => Items.Count > 1;
        public bool CanSelectPreviousSourceRow => CanNavigateSourceRows && _selectedSourceRowIndex > 0;
        public bool CanSelectNextSourceRow => CanNavigateSourceRows
            && _selectedSourceRowIndex >= 0 && _selectedSourceRowIndex < Items.Count - 1;
        public string SourceRowPositionText => Items.Count == 0
            ? TemplateKind.ToString().ToUpperInvariant() + " DESIGN SAMPLE"
            : _selectedSourceRowIndex < 0
                ? "No source row selected"
                : "Source row " + (_selectedSourceRowIndex + 1).ToString("N0", CultureInfo.CurrentCulture)
                    + " of " + Items.Count.ToString("N0", CultureInfo.CurrentCulture);

        public StickerTemplateChoice? SelectedTemplateChoice
        {
            get => _selectedTemplateChoice;
            set
            {
                if (value != null) SelectTemplateChoice(value);
            }
        }

        public void SelectTemplateChoice(StickerTemplateChoice choice)
        {
            if (choice == null || ReferenceEquals(_selectedTemplateChoice, choice)) return;
            ReplaceActiveTemplate(choice.Template, choice, markDirty: false);
        }

        public string TemplateNameText
        {
            get => _activeTemplate.Name;
            set
            {
                string next = value ?? string.Empty;
                if (_activeTemplate.Name == next) return;
                _activeTemplate.Name = next;
                _selectedTemplateChoice?.UpdateTemplateName(next);
                OnPropertyChanged();
                MarkTemplateDirty();
            }
        }

        public StickerTemplateKind TemplateKind
        {
            get => _activeTemplate.Kind;
            set
            {
                if (!Enum.IsDefined(typeof(StickerTemplateKind), value)
                    || _activeTemplate.Kind == value) return;
                _activeTemplate.Kind = value;
                OnPropertyChanged();
                UpdateSamplePreviewForTemplate();
                MarkTemplateDirty();
            }
        }

        public string TemplateBackgroundText
        {
            get => _activeTemplate.Background;
            set
            {
                string next = value ?? string.Empty;
                if (_activeTemplate.Background == next) return;
                _activeTemplate.Background = next;
                OnPropertyChanged();
                MarkTemplateDirty();
            }
        }

        public StickerPrinterModel PrinterModel
        {
            get => _printerModel;
            set
            {
                StickerPrinterModel next = Enum.IsDefined(typeof(StickerPrinterModel), value)
                    ? value : StickerPrinterModel.Custom;
                if (_printerModel == next) return;
                _printerModel = next;
                if (next == StickerPrinterModel.ZebraZt411
                    && _outputLayoutMode == StickerOutputLayoutMode.A4Sheet)
                {
                    _outputLayoutMode = StickerOutputLayoutMode.PrinterDefault;
                    OnPropertyChanged(nameof(OutputLayoutMode));
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsZt411Profile));
                OnPropertyChanged(nameof(OutputLayoutOptions));
                OnPropertyChanged(nameof(PrinterLimitsText));
                OnPropertyChanged(nameof(PrinterSizeWarningText));
                OnPropertyChanged(nameof(HasPrinterSizeWarning));
                NotifySheetLayoutChanged();
                StatusMessage = "Printer profile changed. Save setup or print to retain the selection.";
            }
        }

        public StickerOutputLayoutMode OutputLayoutMode
        {
            get => _outputLayoutMode;
            set
            {
                StickerOutputLayoutMode next = Enum.IsDefined(typeof(StickerOutputLayoutMode), value)
                    ? value : StickerOutputLayoutMode.PrinterDefault;
                if (IsZt411Profile && next == StickerOutputLayoutMode.A4Sheet)
                    next = StickerOutputLayoutMode.PrinterDefault;
                if (_outputLayoutMode == next) return;
                _outputLayoutMode = next;
                OnPropertyChanged();
                NotifySheetLayoutChanged();
                StatusMessage = "Page layout changed. Review the sheet estimate, then save setup or print.";
            }
        }

        public bool IsA4SheetLayout => OutputLayoutMode == StickerOutputLayoutMode.A4Sheet
            || (OutputLayoutMode == StickerOutputLayoutMode.PrinterDefault && !IsZt411Profile);

        public string SheetMarginMmText
        {
            get => _sheetMarginMmText;
            set
            {
                if (!Set(ref _sheetMarginMmText, value ?? string.Empty, notifyBatch: false)) return;
                NotifySheetLayoutChanged();
            }
        }

        public string SheetGapMmText
        {
            get => _sheetGapMmText;
            set
            {
                if (!Set(ref _sheetGapMmText, value ?? string.Empty, notifyBatch: false)) return;
                NotifySheetLayoutChanged();
            }
        }

        public string SheetLayoutSummaryText
        {
            get
            {
                if (!IsA4SheetLayout)
                    return "One physical sticker is placed on each custom-size page. This is the recommended mode for the ZT411 and roll stock.";
                if (!TryGetNominalSheetPlan(out var plan, out string error)) return error;
                long labels = EstimatedLabelCount;
                string batchEstimate = Items.Count == 0
                    ? "Design-only: no source rows are attached; "
                    : labels == 0
                        ? "No valid source labels; "
                        : labels.ToString("N0", CultureInfo.CurrentCulture) + " label(s) ≈ "
                            + plan.PageCountFor(labels).ToString("N0", CultureInfo.CurrentCulture) + " A4 sheet(s); ";
                return "A4 estimate: " + plan.Columns + " columns × " + plan.Rows + " rows ("
                    + plan.LabelsPerSheet + " stickers per sheet); " + batchEstimate
                    + plan.Orientation.ToString().ToLowerInvariant()
                    + ". Actual capacity is recalculated from the selected printer's imageable area.";
            }
        }

        public string EstimatedPrintPlanSummaryText
        {
            get
            {
                long labels = EstimatedLabelCount;
                if (!IsA4SheetLayout)
                    return labels.ToString("N0", CultureInfo.CurrentCulture) + " sticker(s) · one custom-size page each";
                if (!TryGetNominalSheetPlan(out var plan, out _))
                    return labels.ToString("N0", CultureInfo.CurrentCulture) + " sticker(s) · A4 layout needs attention";
                return labels.ToString("N0", CultureInfo.CurrentCulture) + " sticker(s) · about "
                    + plan.PageCountFor(labels).ToString("N0", CultureInfo.CurrentCulture) + " A4 sheet(s)";
            }
        }

        public StickerPrinterResolution PrinterResolution
        {
            get => _printerResolution;
            set
            {
                StickerPrinterResolution next = Enum.IsDefined(typeof(StickerPrinterResolution), value)
                    ? value : StickerPrinterResolution.Dpi203;
                if (_printerResolution == next) return;
                _printerResolution = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrinterLimitsText));
                OnPropertyChanged(nameof(PrinterSizeWarningText));
                OnPropertyChanged(nameof(HasPrinterSizeWarning));
                StatusMessage = "Printer profile changed. Save setup or print to retain the selection.";
            }
        }

        public StickerMediaHandling MediaHandling
        {
            get => _mediaHandling;
            set
            {
                StickerMediaHandling next = Enum.IsDefined(typeof(StickerMediaHandling), value)
                    ? value : StickerMediaHandling.TearOff;
                if (_mediaHandling == next) return;
                _mediaHandling = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrinterLimitsText));
                OnPropertyChanged(nameof(PrinterSizeWarningText));
                OnPropertyChanged(nameof(HasPrinterSizeWarning));
                StatusMessage = "Printer profile changed. Save setup or print to retain the selection.";
            }
        }

        public bool IsZt411Profile => PrinterModel == StickerPrinterModel.ZebraZt411;
        public string PrinterLimitsText => StickerPrinterSpecifications.GetLimits(
            PrinterModel, PrinterResolution, MediaHandling).Description;
        public string PrinterSizeWarningText
        {
            get
            {
                if (!TryParseNumber(WidthMmText, out double width)
                    || !TryParseNumber(HeightMmText, out double height))
                    return "Enter valid numeric width and length values.";
                return StickerPrinterSpecifications.TryValidateDimensions(PrinterModel,
                    PrinterResolution, MediaHandling, width, height, out string error)
                    ? string.Empty : error;
            }
        }
        public bool HasPrinterSizeWarning => !string.IsNullOrWhiteSpace(PrinterSizeWarningText);

        public void ApplyZt411FourBySixStock()
        {
            PrinterModel = StickerPrinterModel.ZebraZt411;
            WidthMmText = Format(101.6);
            HeightMmText = Format(152.4);
            StatusMessage = "Applied common 4 × 6 in stock (101.6 × 152.4 mm). Both dimensions remain manually editable.";
        }

        public StickerLayoutElement? SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (ReferenceEquals(_selectedElement, value)) return;
                _selectedElement = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedElement));
                OnPropertyChanged(nameof(CanMoveSelectedElement));
            }
        }

        public bool HasSelectedElement => SelectedElement != null;
        public bool CanMoveSelectedElement => SelectedElement != null && !SelectedElement.IsLocked;

        /// <summary>Active studio workspace tab. This is UI state only; it is not part of the saved template.</summary>
        public int SelectedStudioTabIndex
        {
            get => _selectedStudioTabIndex;
            set => Set(ref _selectedStudioTabIndex, Math.Clamp(value, 0, 3), notifyBatch: false);
        }
        public string NewFieldKey
        {
            get => _newFieldKey;
            set => Set(ref _newFieldKey, value ?? string.Empty, notifyBatch: false);
        }
        public bool IsTemplateDirty
        {
            get => _isTemplateDirty;
            private set => Set(ref _isTemplateDirty, value, notifyBatch: false);
        }

        public GlassStickerLabel? SelectedLabel
        {
            get => _selectedLabel ?? _previewLabel;
            set
            {
                if (value == null && Items.Count == 0 && _previewLabel != null)
                    value = _previewLabel;
                if (ReferenceEquals(_selectedLabel, value)) return;

                _selectedLabel = value;
                OnPropertyChanged();
                int nextIndex = value == null ? -1 : Items.IndexOf(value);
                if (_selectedSourceRowIndex != nextIndex)
                {
                    _selectedSourceRowIndex = nextIndex;
                    OnPropertyChanged(nameof(SourceRowPositionText));
                    OnPropertyChanged(nameof(CanSelectPreviousSourceRow));
                    OnPropertyChanged(nameof(CanSelectNextSourceRow));
                }
            }
        }

        public void SelectPreviousSourceRow()
        {
            if (CanSelectPreviousSourceRow)
                SelectedLabel = Items[_selectedSourceRowIndex - 1];
        }

        public void SelectNextSourceRow()
        {
            if (CanSelectNextSourceRow)
                SelectedLabel = Items[_selectedSourceRowIndex + 1];
        }

        public string WidthMmText
        {
            get => _widthMmText;
            set
            {
                if (!Set(ref _widthMmText, value ?? string.Empty)) return;
                OnPropertyChanged(nameof(PrinterSizeWarningText));
                OnPropertyChanged(nameof(HasPrinterSizeWarning));
                NotifySheetLayoutChanged();
                if (TryParseNumber(_widthMmText, out double width)
                    && width >= StickerPrinterSpecifications.GenericMinimumWidthMm
                    && width <= StickerPrinterSpecifications.GenericMaximumWidthMm)
                {
                    _activeTemplate.LabelWidthMm = width;
                    MarkTemplateDirty();
                }
            }
        }

        public string HeightMmText
        {
            get => _heightMmText;
            set
            {
                if (!Set(ref _heightMmText, value ?? string.Empty)) return;
                OnPropertyChanged(nameof(PrinterSizeWarningText));
                OnPropertyChanged(nameof(HasPrinterSizeWarning));
                NotifySheetLayoutChanged();
                if (TryParseNumber(_heightMmText, out double height)
                    && height >= StickerPrinterSpecifications.MinimumTemplateLengthMm
                    && height <= StickerPrinterSpecifications.MaximumTemplateLengthMm)
                {
                    _activeTemplate.LabelHeightMm = height;
                    MarkTemplateDirty();
                }
            }
        }

        public string MarginMmText
        {
            get => _marginMmText;
            set
            {
                if (!Set(ref _marginMmText, value ?? string.Empty)) return;
                if (TryParseNumber(_marginMmText, out double margin) && margin >= 0 && margin <= 25
                    && margin * 2 < Math.Min(_activeTemplate.LabelWidthMm, _activeTemplate.LabelHeightMm))
                {
                    _activeTemplate.MarginMm = margin;
                    MarkTemplateDirty();
                }
            }
        }

        public string FontSizePtText
        {
            get => _fontSizePtText;
            set
            {
                if (!Set(ref _fontSizePtText, value ?? string.Empty)) return;
                if (TryParseNumber(_fontSizePtText, out double size) && size >= 3 && size <= 72)
                {
                    _activeTemplate.DefaultFontSizePt = size;
                    MarkTemplateDirty();
                }
            }
        }

        public bool OneLabelPerPiece
        {
            get => _oneLabelPerPiece;
            set
            {
                if (!Set(ref _oneLabelPerPiece, value)) return;
                NotifySheetLayoutChanged();
            }
        }
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
                    if (row == null || !double.IsFinite(row.WidthMm) || !double.IsFinite(row.HeightMm)
                        || row.WidthMm <= 0 || row.HeightMm <= 0 || row.Quantity <= 0)
                        continue;
                    count += OneLabelPerPiece ? row.Quantity : 1;
                    if (count > MaximumBatchSize) return count;
                }
                return count;
            }
        }

        public long EstimatedA4SheetCount
        {
            get
            {
                if (!IsA4SheetLayout || !TryGetNominalSheetPlan(out var plan, out _)) return 0;
                return plan.PageCountFor(EstimatedLabelCount);
            }
        }

        private bool TryGetNominalSheetPlan(out StickerSheetLayoutPlan plan, out string error)
        {
            plan = default;
            error = string.Empty;
            if (!TryParseNumber(WidthMmText, out double width)
                || !TryParseNumber(HeightMmText, out double height))
            {
                error = "Enter valid sticker dimensions to estimate an A4 layout.";
                return false;
            }
            if (!TryParseNumber(SheetMarginMmText, out double margin) || margin < 0 || margin > 25)
            {
                error = "A4 sheet inset must be between 0 and 25 mm.";
                return false;
            }
            if (!TryParseNumber(SheetGapMmText, out double gap) || gap < 0 || gap > 25)
            {
                error = "Sticker gap must be between 0 and 25 mm.";
                return false;
            }
            return StickerSheetLayoutPlanner.TryCreateNominalA4Plan(
                width, height, margin, gap, out plan, out error);
        }

        private void NotifySheetLayoutChanged()
        {
            OnPropertyChanged(nameof(IsA4SheetLayout));
            OnPropertyChanged(nameof(SheetLayoutSummaryText));
            OnPropertyChanged(nameof(EstimatedPrintPlanSummaryText));
            OnPropertyChanged(nameof(EstimatedA4SheetCount));
        }

        public void CreateNewTemplate()
        {
            ReplaceActiveTemplate(StickerTemplatePresets.CreateCustom(), null, markDirty: true);
            TemplateNameText = "New custom template";
            StatusMessage = "Blank template created. Add text or source fields, drag to position, then save as .pgsticker.";
        }

        public void AddTextElement()
        {
            var element = new StickerLayoutElement
            {
                Name = "Text",
                Kind = StickerTemplateElementKind.Text,
                Text = "New text",
                Xmm = _activeTemplate.MarginMm,
                Ymm = _activeTemplate.MarginMm,
                WidthMm = Math.Max(12, _activeTemplate.LabelWidthMm - (_activeTemplate.MarginMm * 2)),
                HeightMm = 8,
                FontSizePt = _activeTemplate.DefaultFontSizePt,
                HideWhenEmpty = false
            };
            AddElement(element);
        }

        public void AddFieldElement()
        {
            var field = StickerTemplateFieldCatalog.Find(NewFieldKey);
            if (field == null) return;
            if (field.IsImage)
            {
                AddLogoElement();
                return;
            }
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                AddTextElement();
                return;
            }
            AddElement(new StickerLayoutElement
            {
                Name = field.Label,
                Kind = StickerTemplateElementKind.Text,
                FieldKey = field.Key,
                Xmm = _activeTemplate.MarginMm,
                Ymm = Math.Clamp(_activeTemplate.MarginMm + (TemplateElements.Count * 7),
                    _activeTemplate.MarginMm,
                    Math.Max(_activeTemplate.MarginMm,
                        _activeTemplate.LabelHeightMm - _activeTemplate.MarginMm - 7)),
                WidthMm = Math.Max(12, _activeTemplate.LabelWidthMm - (_activeTemplate.MarginMm * 2)),
                HeightMm = 7,
                FontSizePt = _activeTemplate.DefaultFontSizePt,
                HideWhenEmpty = true
            });
        }

        public void AddLogoElement()
        {
            AddElement(new StickerLayoutElement
            {
                Name = "Company logo",
                Kind = StickerTemplateElementKind.Logo,
                FieldKey = "CompanyLogo",
                Xmm = _activeTemplate.MarginMm,
                Ymm = _activeTemplate.MarginMm,
                WidthMm = 20,
                HeightMm = 14,
                HideWhenEmpty = false
            });
        }

        public void AddRectangleElement()
        {
            AddElement(new StickerLayoutElement
            {
                Name = "Box",
                Kind = StickerTemplateElementKind.Rectangle,
                Xmm = _activeTemplate.MarginMm,
                Ymm = _activeTemplate.MarginMm,
                WidthMm = Math.Max(0.5, Math.Min(40,
                    _activeTemplate.LabelWidthMm - _activeTemplate.MarginMm * 2)),
                HeightMm = Math.Max(0.5, Math.Min(15,
                    _activeTemplate.LabelHeightMm - _activeTemplate.MarginMm * 2)),
                Background = "#00FFFFFF",
                BorderColor = "#FF344054",
                BorderWidthMm = 0.3
            });
        }

        public void AddLineElement()
        {
            AddElement(new StickerLayoutElement
            {
                Name = "Divider line",
                Kind = StickerTemplateElementKind.Line,
                Xmm = _activeTemplate.MarginMm,
                Ymm = _activeTemplate.MarginMm,
                WidthMm = Math.Max(10, _activeTemplate.LabelWidthMm - (_activeTemplate.MarginMm * 2)),
                HeightMm = 0.7,
                Background = "#FF344054",
                BorderColor = "#FF344054"
            });
        }

        public void DuplicateSelectedElement()
        {
            if (SelectedElement == null) return;
            var copy = SelectedElement.DeepClone();
            copy.Name = SelectedElement.Name + " copy";
            copy.Xmm = Math.Min(_activeTemplate.LabelWidthMm - copy.WidthMm,
                SelectedElement.Xmm + 2);
            copy.Ymm = Math.Min(_activeTemplate.LabelHeightMm - copy.HeightMm,
                SelectedElement.Ymm + 2);
            copy.IsLocked = false;
            AddElement(copy);
        }

        public void DeleteSelectedElement()
        {
            if (SelectedElement == null) return;
            int index = TemplateElements.IndexOf(SelectedElement);
            TemplateElements.Remove(SelectedElement);
            SelectedElement = TemplateElements.Count == 0
                ? null
                : TemplateElements[Math.Min(index, TemplateElements.Count - 1)];
        }

        public void MoveSelectedElementLayer(int direction)
        {
            if (SelectedElement == null || direction == 0) return;
            int index = TemplateElements.IndexOf(SelectedElement);
            int target = Math.Clamp(index + Math.Sign(direction), 0, TemplateElements.Count - 1);
            if (target != index) TemplateElements.Move(index, target);
        }

        public void NudgeSelectedElement(double dxMm, double dyMm)
        {
            if (SelectedElement == null || SelectedElement.IsLocked) return;
            SetSelectedElementPosition(
                SelectedElement.Xmm + dxMm,
                SelectedElement.Ymm + dyMm);
        }

        public void SetSelectedElementPosition(double xMm, double yMm)
        {
            if (SelectedElement == null || SelectedElement.IsLocked) return;
            SelectedElement.Xmm = Math.Clamp(xMm, 0, Math.Max(0, _activeTemplate.LabelWidthMm - SelectedElement.WidthMm));
            SelectedElement.Ymm = Math.Clamp(yMm, 0, Math.Max(0, _activeTemplate.LabelHeightMm - SelectedElement.HeightMm));
        }

        public void LoadTemplateFile(StickerLayoutTemplate template, string path)
        {
            var choice = new StickerTemplateChoice(template.Name + " · file", template, false, path);
            AddOrReplaceChoice(choice);
            _selectedTemplateChoice = choice;
            OnPropertyChanged(nameof(SelectedTemplateChoice));
            ReplaceActiveTemplate(template, choice, markDirty: false);
            StatusMessage = "Loaded template file: " + Path.GetFileName(path);
        }

        public bool TrySaveActiveTemplateToLibrary(out string path, out string error)
        {
            path = string.Empty;
            if (!TryPrepareTemplateForSave(out error)) return false;
            if (!StickerTemplateStore.TrySaveToLibrary(_activeTemplate, out path, out error)) return false;
            RegisterSavedTemplate(path);
            return true;
        }

        public bool TryPrepareTemplateForSave(out string error)
        {
            error = string.Empty;
            if (!TryParseNumber(_widthMmText, out double width)
                || width < StickerPrinterSpecifications.GenericMinimumWidthMm
                || width > StickerPrinterSpecifications.GenericMaximumWidthMm)
            {
                error = "Label width must be between 15 and 500 mm.";
                return false;
            }
            if (!TryParseNumber(_heightMmText, out double height)
                || height < StickerPrinterSpecifications.MinimumTemplateLengthMm
                || height > StickerPrinterSpecifications.MaximumTemplateLengthMm)
            {
                error = "Label length must be between 12.7 and 991 mm.";
                return false;
            }
            if (!TryParseNumber(_marginMmText, out double margin) || margin < 0 || margin > 25
                || margin * 2 >= Math.Min(width, height))
            {
                error = "Margin must be between 0 and 25 mm and leave printable space.";
                return false;
            }
            if (!TryParseNumber(_fontSizePtText, out double fontSize) || fontSize < 3 || fontSize > 72)
            {
                error = "Default font size must be between 3 and 72 pt.";
                return false;
            }

            _activeTemplate.LabelWidthMm = width;
            _activeTemplate.LabelHeightMm = height;
            _activeTemplate.MarginMm = margin;
            _activeTemplate.DefaultFontSizePt = fontSize;
            return StickerTemplateStore.TryValidate(_activeTemplate, out error);
        }

        public void RegisterExportedTemplate(string path)
        {
            RegisterSavedTemplate(path);
        }

        public void SetTemplateSavedMessage(string message) => StatusMessage = message;
        public void MarkTemplatePersisted() => IsTemplateDirty = false;

        public bool TryCreatePreferences(out StickerPrintPreferences preferences, out string error,
            bool requirePrintableRows = true)
        {
            preferences = new StickerPrintPreferences();
            error = string.Empty;

            if (!TryParseNumber(WidthMmText, out double width)
                || width < StickerPrinterSpecifications.GenericMinimumWidthMm
                || width > StickerPrinterSpecifications.GenericMaximumWidthMm)
            {
                error = "Label width must be between 15 and 500 mm.";
                return false;
            }
            if (!TryParseNumber(HeightMmText, out double height)
                || height < StickerPrinterSpecifications.MinimumTemplateLengthMm
                || height > StickerPrinterSpecifications.MaximumTemplateLengthMm)
            {
                error = "Label length must be between 12.7 and 991 mm.";
                return false;
            }
            if (!StickerPrinterSpecifications.TryValidateDimensions(PrinterModel,
                    PrinterResolution, MediaHandling, width, height, out error))
                return false;
            if (!TryParseNumber(MarginMmText, out double margin)
                || margin < 0 || margin > 25 || margin * 2 >= Math.Min(width, height))
            {
                error = "Margin must be between 0 and 25 mm and leave printable space.";
                return false;
            }
            if (!TryParseNumber(SheetMarginMmText, out double sheetMargin) || sheetMargin < 0 || sheetMargin > 25)
            {
                error = "A4 sheet inset must be between 0 and 25 mm.";
                return false;
            }
            if (!TryParseNumber(SheetGapMmText, out double sheetGap) || sheetGap < 0 || sheetGap > 25)
            {
                error = "Sticker gap must be between 0 and 25 mm.";
                return false;
            }
            if (IsA4SheetLayout && !TryGetNominalSheetPlan(out _, out error))
                return false;
            if (!TryParseNumber(FontSizePtText, out double fontSize) || fontSize < 3 || fontSize > 72)
            {
                error = "Default font size must be between 3 and 72 pt.";
                return false;
            }
            if (requirePrintableRows && (Items.Count == 0 || EstimatedLabelCount == 0))
            {
                error = "There are no valid glass panes to print.";
                return false;
            }
            if (requirePrintableRows && EstimatedLabelCount > MaximumBatchSize)
            {
                error = "This batch exceeds 50,000 labels. Filter or split the source job before printing.";
                return false;
            }

            _activeTemplate.LabelWidthMm = width;
            _activeTemplate.LabelHeightMm = height;
            _activeTemplate.MarginMm = margin;
            _activeTemplate.DefaultFontSizePt = fontSize;
            if (!StickerTemplateStore.TryValidate(_activeTemplate, out error)) return false;
            preferences = new StickerPrintPreferences
            {
                LabelWidthMm = width,
                LabelHeightMm = height,
                MarginMm = margin,
                FontSizePt = fontSize,
                LastTemplateName = _activeTemplate.Name,
                LastTemplate = _activeTemplate.DeepClone(),
                PrinterModel = PrinterModel,
                OutputLayoutMode = OutputLayoutMode,
                PrinterResolution = PrinterResolution,
                MediaHandling = MediaHandling,
                SheetMarginMm = sheetMargin,
                SheetGapMm = sheetGap,
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

        private void UpdateSamplePreviewForTemplate()
        {
            if (Items.Count > 0) return;

            _previewLabel = StickerTemplateSampleFactory.Create(_activeTemplate.Kind);
            _selectedLabel = _previewLabel;
            OnPropertyChanged(nameof(SelectedLabel));
            OnPropertyChanged(nameof(SourceRowPositionText));
        }

        private void ReplaceActiveTemplate(StickerLayoutTemplate template,
            StickerTemplateChoice? choice, bool markDirty)
        {
            RestoreSelectedChoiceName();
            DetachTemplateEvents();
            _loadingTemplate = true;
            _activeTemplate = template.DeepClone();
            _selectedElement = null;
            _widthMmText = Format(_activeTemplate.LabelWidthMm);
            _heightMmText = Format(_activeTemplate.LabelHeightMm);
            _marginMmText = Format(_activeTemplate.MarginMm);
            _fontSizePtText = Format(_activeTemplate.DefaultFontSizePt);
            _selectedTemplateChoice = choice;
            _selectedTemplateChoice?.UpdateTemplateName(_activeTemplate.Name);
            if (Items.Count == 0)
                UpdateSamplePreviewForTemplate();
            else if (_selectedLabel != null)
                _selectedSourceRowIndex = Items.IndexOf(_selectedLabel);
            AttachTemplateEvents();
            _loadingTemplate = false;
            OnPropertyChanged(nameof(ActiveTemplate));
            OnPropertyChanged(nameof(TemplateElements));
            OnPropertyChanged(nameof(TemplateNameText));
            OnPropertyChanged(nameof(TemplateKind));
            OnPropertyChanged(nameof(TemplateBackgroundText));
            OnPropertyChanged(nameof(SelectedTemplateChoice));
            OnPropertyChanged(nameof(SelectedElement));
            OnPropertyChanged(nameof(HasSelectedElement));
            OnPropertyChanged(nameof(CanMoveSelectedElement));
            OnPropertyChanged(nameof(WidthMmText));
            OnPropertyChanged(nameof(HeightMmText));
            OnPropertyChanged(nameof(MarginMmText));
            OnPropertyChanged(nameof(FontSizePtText));
            NotifySheetLayoutChanged();
            if (markDirty) MarkTemplateDirty();
            else IsTemplateDirty = false;
        }

        private void AddElement(StickerLayoutElement element)
        {
            TemplateElements.Add(element);
            SelectedElement = element;
        }

        private void ApplyLegacyFieldVisibility(StickerPrintPreferences settings)
        {
            SetVisible("CompanyLogo", settings.ShowLogo);
            SetVisible("CompanyName", settings.ShowCompanyName);
            SetVisible("CompanyAddress", settings.ShowAddress);
            SetVisible("CompanyTelephone", settings.ShowTelephone);
            SetVisible("CompanyMobile", settings.ShowMobile);
            SetVisible("CompanyWebsite", settings.ShowWebsite);
            SetVisible("CompanyEmail", settings.ShowEmail);
            SetVisible("OrderDisplay", settings.ShowOrder);
            SetVisible("CustomerCompanyName", settings.ShowCustomer);
            SetVisible("GlassName", settings.ShowGlassName);
            SetVisible("SizeText", settings.ShowSize);
            SetVisible("Quantity", settings.ShowQuantity);
            SetVisible("Reference", settings.ShowReference);
            SetVisible("PanePosition", settings.ShowPanePosition);
        }

        private void SetVisible(string fieldKey, bool visible)
        {
            foreach (var element in _activeTemplate.Elements.Where(e =>
                         string.Equals(e.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase)))
                element.IsVisible = visible;
        }

        private void AddOrReplaceChoice(StickerTemplateChoice choice)
        {
            for (int i = TemplateChoices.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(choice.FilePath)
                    && string.Equals(TemplateChoices[i].FilePath, choice.FilePath,
                        StringComparison.OrdinalIgnoreCase))
                    TemplateChoices.RemoveAt(i);
            }
            TemplateChoices.Add(choice);
        }

        private void RestoreSelectedChoiceName()
        {
            if (_selectedTemplateChoice != null)
                _selectedTemplateChoice.UpdateTemplateName(_selectedTemplateChoice.Template.Name);
        }

        private void RegisterSavedTemplate(string path)
        {
            RestoreSelectedChoiceName();
            var choice = new StickerTemplateChoice(_activeTemplate.Name + " · saved",
                _activeTemplate.DeepClone(), isBuiltIn: false, filePath: path);
            AddOrReplaceChoice(choice);
            _selectedTemplateChoice = choice;
            OnPropertyChanged(nameof(SelectedTemplateChoice));
            IsTemplateDirty = false;
            OnPropertyChanged(nameof(TemplateNameText));
        }

        private void AttachTemplateEvents()
        {
            _activeTemplate.Elements.CollectionChanged += OnTemplateElementsChanged;
            foreach (var element in _activeTemplate.Elements)
                element.PropertyChanged += OnTemplateElementChanged;
        }

        private void DetachTemplateEvents()
        {
            _activeTemplate.Elements.CollectionChanged -= OnTemplateElementsChanged;
            foreach (var element in _activeTemplate.Elements)
                element.PropertyChanged -= OnTemplateElementChanged;
        }

        private void OnTemplateElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (StickerLayoutElement element in e.OldItems)
                    element.PropertyChanged -= OnTemplateElementChanged;
            if (e.NewItems != null)
                foreach (StickerLayoutElement element in e.NewItems)
                    element.PropertyChanged += OnTemplateElementChanged;
            OnPropertyChanged(nameof(TemplateElements));
            OnPropertyChanged(nameof(EditorRevision));
            if (!_loadingTemplate) MarkTemplateDirty();
        }

        private void OnTemplateElementChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ReferenceEquals(sender, SelectedElement) && e.PropertyName == nameof(StickerLayoutElement.IsLocked))
                OnPropertyChanged(nameof(CanMoveSelectedElement));
            OnPropertyChanged(nameof(EditorRevision));
            if (!_loadingTemplate) MarkTemplateDirty();
        }

        public int EditorRevision => 0;

        private void MarkTemplateDirty()
        {
            IsTemplateDirty = true;
            OnPropertyChanged(nameof(ActiveTemplate));
            OnPropertyChanged(nameof(EditorRevision));
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
