using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    /// <summary>
    /// ViewModel for the Add/Edit sheet dialog.
    /// Holds form field values + validation + result.
    /// </summary>
    public class AddEditSheetViewModel : ViewModelBase
    {
        private readonly GlassSheet? _existing;

        public bool IsEditMode => _existing != null;
        public string DialogTitle => IsEditMode ? "Edit Glass Sheet" : "Add Glass Sheet";
        public string SaveButtonText => IsEditMode ? "Update" : "Add";

        // ============ FORM FIELDS ============
        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private int _thicknessMm = 6;
        public int ThicknessMm
        {
            get => _thicknessMm;
            set { _thicknessMm = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private string _color = "Clear";
        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private int _widthMm = 3210;
        public int WidthMm
        {
            get => _widthMm;
            set { _widthMm = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private int _heightMm = 2250;
        public int HeightMm
        {
            get => _heightMm;
            set { _heightMm = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private string _supplier = string.Empty;
        public string Supplier
        {
            get => _supplier;
            set { _supplier = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private decimal _purchasePrice;
        public decimal PurchasePricePerSqm
        {
            get => _purchasePrice;
            set { _purchasePrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private decimal _sellPrice;
        public decimal SellPricePerSqm
        {
            get => _sellPrice;
            set { _sellPrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        private int _stock;
        public int Stock
        {
            get => _stock;
            set { _stock = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        // ============ VALIDATION ============
        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Category) &&
            ThicknessMm > 0 &&
            !string.IsNullOrWhiteSpace(Color) &&
            WidthMm > 0 && HeightMm > 0 &&
            !string.IsNullOrWhiteSpace(Supplier) &&
            PurchasePricePerSqm >= 0 && SellPricePerSqm >= 0 &&
            Stock >= 0;

        // ============ RESULT ============
        public GlassSheet? Result { get; private set; }
        public bool? DialogResult { get; private set; }

        // ============ COMMANDS ============
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public Action? CloseAction { get; set; }

        // ============ CONSTRUCTORS ============
        public AddEditSheetViewModel() : this(null) { }

        public AddEditSheetViewModel(GlassSheet? existing)
        {
            _existing = existing;

            if (existing != null)
            {
                Category = existing.Category;
                ThicknessMm = existing.ThicknessMm;
                Color = existing.Color;
                WidthMm = existing.WidthMm;
                HeightMm = existing.HeightMm;
                Supplier = existing.Supplier;
                PurchasePricePerSqm = existing.PurchasePricePerSqm;
                SellPricePerSqm = existing.SellPricePerSqm;
                Stock = existing.Stock;
            }

            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        private void Save()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(Category))
            {
                ErrorMessage = "Category is required.";
                return;
            }

            if (ThicknessMm <= 0)
            {
                ErrorMessage = "Thickness must be greater than 0.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Color))
            {
                ErrorMessage = "Color is required.";
                return;
            }

            if (WidthMm <= 0 || HeightMm <= 0)
            {
                ErrorMessage = "Width and Height must be greater than 0.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Supplier))
            {
                ErrorMessage = "Supplier is required.";
                return;
            }

            if (PurchasePricePerSqm < 0 || SellPricePerSqm < 0)
            {
                ErrorMessage = "Prices cannot be negative.";
                return;
            }

            if (Stock < 0)
            {
                ErrorMessage = "Stock cannot be negative.";
                return;
            }

            // All valid — build result
            Result = _existing ?? new GlassSheet();
            Result.Category = Category.Trim();
            Result.ThicknessMm = ThicknessMm;
            Result.Color = Color.Trim();
            Result.WidthMm = WidthMm;
            Result.HeightMm = HeightMm;
            Result.Supplier = Supplier.Trim();
            Result.PurchasePricePerSqm = PurchasePricePerSqm;
            Result.SellPricePerSqm = SellPricePerSqm;
            Result.Stock = Stock;

            DialogResult = true;
            CloseAction?.Invoke();
        }

        private void Cancel()
        {
            DialogResult = false;
            CloseAction?.Invoke();
        }
    }
}