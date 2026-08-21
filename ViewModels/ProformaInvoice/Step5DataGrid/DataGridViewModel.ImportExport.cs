using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.ProformaInvoice.Step5Grid;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    public partial class DataGridViewModel
    {
        public ICommand ImportCutListCommand { get; private set; }
        public ICommand ExportExcelCommand { get; private set; }
        public ICommand ExportCsvCommand { get; private set; }
        public ICommand ExportPdfCommand { get; private set; }

        private string _ioStatus = "";
        public string IoStatus
        {
            get => _ioStatus;
            private set
            {
                if (_ioStatus == value) return;
                _ioStatus = value ?? "";
                OnPropertyChanged();
            }
        }

        private void WireImportExportCommands()
        {
            ImportCutListCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ImportCutListAsync, "PI Step5"));
            ExportExcelCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() =>
                CutListGridIo.ExportExcelAsync(SpecGroups, CurrentDocumentId()), "PI Step5"));
            ExportCsvCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() =>
                CutListGridIo.ExportCsvAsync(SpecGroups, CurrentDocumentId()), "PI Step5"));
            ExportPdfCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() =>
                CutListGridIo.ExportPdfAsync(SpecGroups, CurrentDocumentId()), "PI Step5"));
        }

        private static string CurrentDocumentId()
        {
            try
            {
                return Application.Current?.MainWindow?.DataContext is
                    ProformaInvoice.ProformaInvoiceViewModel shell
                    ? shell.Step1VM?.ProformaId ?? "PI"
                    : "PI";
            }
            catch
            {
                return "PI";
            }
        }

        

        

        private async Task ImportCutListAsync()
        {
            string? path = CutListGridIo.PromptOpenPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            IoStatus = "Importing…";
            var parsed = await CutListGridIo.ImportFileAsync(path);
            if (parsed.Rows.Count == 0)
            {
                IoStatus = parsed.Warning ?? "No rows imported.";
                MessageBox.Show(IoStatus, "Import cut list",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var group = SelectedSpecGroup;
            if (group == null)
            {
                AddNewSpecGroup();
                group = SelectedSpecGroup;
            }

            int added = group!.ApplyImportedRows(parsed.Rows);
            string extra = parsed.Skipped > 0 ? $"  ·  skipped {parsed.Skipped}" : "";
            IoStatus = $"Imported {added} row(s){extra}.";
        }
    }
}
