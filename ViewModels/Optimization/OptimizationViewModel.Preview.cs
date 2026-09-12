// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Preview.cs
// MODULE: Optimization — 2D VIEW — sheet canvas, zoom, filmstrip and piece rectangles (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

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

        // ── 2D VIEW tab ────────────────────────────────────────────────

        public sealed class PieceRectVm
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string Label { get; set; } = string.Empty;
            public bool Rotated { get; set; }
        }

        public ObservableCollection<PieceRectVm> SheetRects { get; } = new();

        private double _renderWidth;
        private double _renderHeight;
        private string _previewCaption = string.Empty;
        private int _selectedSheetIndex;
        private double _previewScale = 1.0;

        public double RenderWidth { get => _renderWidth; private set { _renderWidth = value; OnPropertyChanged(); } }
        public double RenderHeight { get => _renderHeight; private set { _renderHeight = value; OnPropertyChanged(); } }
        public string PreviewCaption { get => _previewCaption; private set { _previewCaption = value; OnPropertyChanged(); } }

        /// <summary>Canvas zoom factor (0.4× .. 2.0×) for the 2D layout view.</summary>
        public double PreviewScale
        {
            get => _previewScale;
            private set
            {
                double clamped = System.Math.Clamp(value, 0.4, 2.0);
                if (_previewScale == clamped) return;
                _previewScale = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewScaleLabel));
                RebuildPreview();
            }
        }

        public string PreviewScaleLabel => (_previewScale * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        // ── 2D chrome: scaled margin zone (usable rectangle) ────────────
        private double _usableX;
        private double _usableY;
        private double _usableW;
        private double _usableH;
        public double UsableX { get => _usableX; private set { _usableX = value; OnPropertyChanged(); } }
        public double UsableY { get => _usableY; private set { _usableY = value; OnPropertyChanged(); } }
        public double UsableW { get => _usableW; private set { _usableW = value; OnPropertyChanged(); } }
        public double UsableH { get => _usableH; private set { _usableH = value; OnPropertyChanged(); } }

        private string _previewStockLabel = string.Empty;
        public string PreviewStockLabel { get => _previewStockLabel; private set { _previewStockLabel = value; OnPropertyChanged(); } }

        private string _previewUtilization = "—";
        public string PreviewUtilization { get => _previewUtilization; private set { _previewUtilization = value; OnPropertyChanged(); } }

        private string _previewPiecesCount = "—";
        public string PreviewPiecesCount { get => _previewPiecesCount; private set { _previewPiecesCount = value; OnPropertyChanged(); } }

        private string _marginZoneLabel = string.Empty;
        public string MarginZoneLabel { get => _marginZoneLabel; private set { _marginZoneLabel = value; OnPropertyChanged(); } }

        private bool _hasPreview;
        public bool HasPreview { get => _hasPreview; private set { _hasPreview = value; OnPropertyChanged(); } }

        public int SelectedSheetIndex
        {
            get => _selectedSheetIndex;
            set
            {
                if (_selectedSheetIndex == value) return;
                _selectedSheetIndex = value;
                OnPropertyChanged();
                RebuildPreview();
            }
        }

        public ICommand SelectPreviousSheetCommand { get; }
        public ICommand SelectNextSheetCommand { get; }
        public ICommand SelectSheetCommand { get; }
        public ICommand PreviewZoomInCommand { get; }
        public ICommand PreviewZoomOutCommand { get; }
        public ICommand PreviewZoomResetCommand { get; }

        // ── 2D preview ─────────────────────────────────────────────────

        private const double PreviewRenderWidth = 860.0;

        private void RebuildPreview()
        {
            SheetRects.Clear();
            RefreshSheetSelection();

            if (_flatSheets.Count == 0 ||
                SelectedSheetIndex < 0 || SelectedSheetIndex >= _flatSheets.Count)
            {
                RenderWidth = 0;
                RenderHeight = 0;
                PreviewCaption = string.Empty;
                PreviewStockLabel = string.Empty;
                PreviewUtilization = "—";
                PreviewPiecesCount = "—";
                MarginZoneLabel = string.Empty;
                HasPreview = false;
                return;
            }

            HasPreview = true;
            var flat = _flatSheets[SelectedSheetIndex];
            var result = flat.Run.Result;
            var sheet = result.Sheets[flat.SheetIndex];
            var margins = flat.Run.Margins;

            double scale = PreviewRenderWidth * PreviewScale / sheet.Stock.WidthMm;
            RenderWidth = PreviewRenderWidth * PreviewScale;
            RenderHeight = sheet.Stock.HeightMm * scale;

            // Margin zone = the usable rectangle inside the edge trims.
            UsableX = sheet.UsableX * scale;
            UsableY = sheet.UsableY * scale;
            UsableW = sheet.UsableWidth * scale;
            UsableH = sheet.UsableHeight * scale;

            MarginZoneLabel = string.Format(CultureInfo.InvariantCulture,
                "margins LM {0} · RM {1} · TM {2} · BM {3} · kerf {4} mm",
                margins.LeftMm, margins.RightMm,
                margins.TopMm, margins.BottomMm, margins.KerfMm);

            PreviewStockLabel = string.Format(CultureInfo.InvariantCulture,
                "{0} × {1} mm", sheet.Stock.WidthMm, sheet.Stock.HeightMm);
            PreviewUtilization = sheet.UtilizationLabel;
            PreviewPiecesCount = sheet.Pieces.Count.ToString(CultureInfo.InvariantCulture);

            foreach (var p in sheet.Pieces)
            {
                string refLabel = string.IsNullOrWhiteSpace(p.Piece.PieceLabel)
                    ? p.Piece.GlassRef : p.Piece.PieceLabel;
                string positionTag = string.IsNullOrWhiteSpace(p.Piece.Position)
                    ? string.Empty : p.Piece.Position + " · ";
                SheetRects.Add(new PieceRectVm
                {
                    X = p.X * scale,
                    Y = p.Y * scale,
                    Width = System.Math.Max(1, p.WidthMm * scale),
                    Height = System.Math.Max(1, p.HeightMm * scale),
                    Rotated = p.Rotated,
                    Label = string.Format(CultureInfo.InvariantCulture, "{0}{1} · {2}×{3}{4}",
                        positionTag, refLabel,
                        p.Piece.WidthMm, p.Piece.HeightMm, p.Rotated ? " ⟳" : string.Empty)
                });
            }

            PreviewCaption = string.Format(CultureInfo.InvariantCulture,
                "{0} — sheet {1} of {2} — stock {3} × {4} mm — {5} pieces",
                flat.Run.Composition, flat.SheetIndex + 1, result.Sheets.Count,
                sheet.Stock.WidthMm, sheet.Stock.HeightMm, sheet.Pieces.Count);
        }

        private void RefreshSheetSelection()
        {
            foreach (var row in SheetRows)
                row.IsSelected = row.Index == SelectedSheetIndex;
        }
    }
}
