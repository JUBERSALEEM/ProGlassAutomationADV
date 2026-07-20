namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    /// <summary>
    /// ViewModel for Step 5 of the Proforma Invoice wizard.
    /// Handles the data grid where users add glass panel line items.
    /// Each spec group contains multiple item rows with 11 columns:
    /// SR | GlassRef | W1 | H1 | W2 | H2 | Qty | SQM | TotalSQM | Rate | Value
    /// Rates from Step 4 (Pricing Estimator) are pushed here via APPLY button.
    /// </summary>
    public class DataGridViewModel : ViewModelBase
    {
        // === Header Info ===
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } = "Specify the number of unique glass specifications in this proforma.";

        // === Total Groups Counter (will grow as user adds spec groups) ===
        private int _totalSpecGroups;
        public int TotalSpecGroups
        {
            get => _totalSpecGroups;
            set { _totalSpecGroups = value; OnPropertyChanged(); }
        }

        public DataGridViewModel()
        {
            // Start with 1 spec group (like the screenshot)
            TotalSpecGroups = 1;

            // Spec groups collection, item rows, calculations will be added
            // in sub-phases 7.2 through 7.12
        }
    }
}