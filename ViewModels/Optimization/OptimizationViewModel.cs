// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Optimization/OptimizationViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — module VM (design 2026-09-12)
//
// Tabs: JOBS → SIZES → STOCKS → SETTINGS → SUMMARY → 2D VIEW → REPORT.
//   • JOBS     — piece list (ref / W×H / qty); a reference job loader
//                ships with the owner's calibration DGU data. The smart
//                Job-Order picker (source lock: confirmed JOs only).
//   • SIZES    — merged thickness+color summary of the piece list.
//   • STOCKS   — Step 4 stock pool: the sheet sizes the selected specs
//                were quoted on (outer/inner per spec), tagged by pane
//                composition so each cutting group packs against its own
//                rack. Racks are ticked "use in run" — never the whole
//                sheet store.
//   • SETTINGS — per-thickness LM/RM/TM/BM, kerf, minimum breakout,
//                rotation (enabled by default for all pieces).
//   • SUMMARY  — sheets, utilization, unplaced, oracle verdict, ordered
//                highest → lowest utilization.
//   • 2D VIEW  — centred sheet canvas with two-tone margin/breakout
//                overlay and prev/next navigation.
//   • REPORT   — layout / parts / stocks summaries with CSV export.
//
// Engine: Services/Optimization/GuillotinePacker (pure, deterministic,
// calibrated against the owner's benchmark: Low-E 17 @ 82.77%).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── tabs ────────────────────────────────────────────────────────
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }
        // ── ctor ───────────────────────────────────────────────────────

        public OptimizationViewModel()
        {
            AddPieceCommand = new RelayCommand(_ => AddPiece());
            RemovePieceCommand = new RelayCommand(p => RemovePiece(p as PieceEntryVm));
            ClearPiecesCommand = new RelayCommand(_ => { Pieces.Clear(); RefreshDerived(); });
            LoadReferenceJobCommand = new RelayCommand(_ => LoadReferenceJob());
            RefreshJobsCommand = new RelayCommand(_ => LoadJobsAsync());
            AddSelectedPanesCommand = new RelayCommand(_ => AddSelectedPanes());
            SelectAllPanesCommand = new RelayCommand(_ => SetAllPanesSelected(true));
            ClearPaneSelectionCommand = new RelayCommand(_ => SetAllPanesSelected(false));
            LoadJobsAsync();

            AddStockCommand = new RelayCommand(_ => AddStock());
            RemoveStockCommand = new RelayCommand(p => RemoveStock(p as StockEntryVm));
            ImportStockFromStep4Command = new RelayCommand(_ => ImportStockFromStep4());

            LoadHouseDefaultsCommand = new RelayCommand(_ => LoadHouseDefaults());

            RunQuickCommand = new RelayCommand(_ => Run(stage: "QUICK"));
            RunOptimizeCommand = new RelayCommand(_ => Run(stage: "OPTIMIZE"));

            ExportLayoutReportCommand = new RelayCommand(p => ExportLayoutReport(ParseExportFormat(p)));
            ExportPartsReportCommand = new RelayCommand(p => ExportPartsReport(ParseExportFormat(p)));
            ExportStocksReportCommand = new RelayCommand(p => ExportStocksReport(ParseExportFormat(p)));
            ExportIndustrialReportCommand = new RelayCommand(p => ExportIndustrialReport(ParseExportFormat(p)));
            ExportAllCommand = new RelayCommand(p => ExportAll(ParseExportFormat(p)));

            SaveSessionCommand = new RelayCommand(_ => SaveSessionAsync());
            OpenSessionCommand = new RelayCommand(p => OpenSession(p as SessionRowVm));
            DeleteSessionCommand = new RelayCommand(p => DeleteSession(p as SessionRowVm));
            RefreshSessionsCommand = new RelayCommand(_ => LoadSavedSessionsAsync());
            _ = LoadSavedSessionsAsync();

            SelectPreviousSheetCommand = new RelayCommand(_ =>
            {
                if (SelectedSheetIndex > 0) SelectedSheetIndex--;
            });
            SelectNextSheetCommand = new RelayCommand(_ =>
            {
                if (SelectedSheetIndex < _flatSheets.Count - 1)
                    SelectedSheetIndex++;
            });
            SelectSheetCommand = new RelayCommand(p =>
            {
                if (p is int idx && idx >= 0 && idx < _flatSheets.Count)
                {
                    SelectedSheetIndex = idx;
                    SelectedTabIndex = 5; // jump straight to the 2D VIEW
                }
            });
            PreviewZoomInCommand = new RelayCommand(_ => PreviewScale += 0.25);
            PreviewZoomOutCommand = new RelayCommand(_ => PreviewScale -= 0.25);
            PreviewZoomResetCommand = new RelayCommand(_ => PreviewScale = 1.0);

            // The stock pool starts EMPTY — it is populated from the ticked
            // Job Orders' Step 4 sheet sizes (STOCKS tab rule). No generic
            // "full rack" is pre-seeded; the run packs each cutting group
            // against the rack its spec was quoted on.
            RefreshDerived();
        }
    }
}
