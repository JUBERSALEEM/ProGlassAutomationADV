using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.ProformaInvoice.Step5Grid;
using System;
using System.Collections.Generic;
using System.Linq;
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

            CutListImportResult parsed;
            try
            {
                // Parse off the UI thread: ClosedXML and large CSV/TXT reads
                // otherwise freeze the grid, which makes import look "stuck"
                // at "Importing…" even when it is still running.
                parsed = await Task.Run(() => CutListGridIo.ImportFileAsync(path))
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                IoStatus = "Import failed.";
                MessageBox.Show(
                    "Could not read the cut list.\n\n" + ex.Message +
                    "\n\nMake sure the file is a valid Excel / CSV / TXT file " +
                    "and is not open in another program.",
                    "Import cut list", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (parsed.Rows.Count == 0)
            {
                IoStatus = parsed.Warning ?? "No rows imported.";
                MessageBox.Show(IoStatus, "Import cut list",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int added = ApplyImportedRowsBySpec(parsed.Rows);
            string extra = parsed.Skipped > 0 ? $"  ·  skipped {parsed.Skipped}" : "";
            IoStatus = added > 0
                ? $"Imported {added} row(s){extra}."
                : parsed.Warning ?? "Nothing was imported.";
        }

        /// <summary>
        /// Splits an imported cut list by its "Spec Name" column (when
        /// present) so a cut list with two specs lands in the matching two
        /// spec groups instead of collapsing into one. Rows without a spec
        /// name fall back to the currently selected group.
        /// </summary>
        private int ApplyImportedRowsBySpec(
            IReadOnlyList<Services.ProformaInvoice.Step5Grid.CutListImportRow> rows)
        {
            if (rows == null || rows.Count == 0) return 0;

            int added = 0;

            var named = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.SpecName))
                .GroupBy(r => r.SpecName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in named)
            {
                var target = SpecGroups.FirstOrDefault(sg =>
                    string.Equals(sg.SelectedSpecName, g.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sg.GroupName, g.Key, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                    target = AddNewSpecGroup(g.Key);

                added += target.ApplyImportedRows(g.ToList());
            }

            var unnamed = rows.Where(r => string.IsNullOrWhiteSpace(r.SpecName)).ToList();
            if (unnamed.Count > 0)
            {
                var group = SelectedSpecGroup;
                if (group == null)
                    group = AddNewSpecGroup();
                added += group!.ApplyImportedRows(unnamed);
            }

            return added;
        }
    }
}
