// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Optimization/OptimizationViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — module VM (design 2026-09-12)
//
// Tabs: JOBS → SIZES → STOCKS → MARGINS → RESULT → 2D VIEW.
//   • JOBS    — piece list (ref / W×H / qty); a reference job loader
//               ships with the owner's calibration DGU data. The smart
//               Job-Order picker arrives next phase (source lock:
//               confirmed JOs only).
//   • SIZES   — merged thickness+color summary of the piece list.
//   • STOCKS  — editable stock pool ("best sheet size" rule: smallest
//               fitting stock is consumed first).
//   • MARGINS — per-thickness LM/RM/TM/BM, kerf, minimum breakout,
//               rotation (enabled by default for all pieces).
//   • RESULT  — sheets, utilization, unplaced, oracle verdict.
//   • 2D VIEW — scaled sheet canvas with piece labels.
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

            foreach (var s in DefaultStockPool()) Stocks.Add(s);
            RefreshDerived();
        }
    }
}
