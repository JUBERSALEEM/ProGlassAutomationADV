// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/DeliveryNote/DeliveryNoteDetail/DnItemRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — detail
//
// Row view-model for one delivered line item. Extracted from the large
// DeliveryNoteDetailViewModel file (Phase 11/12 God-VM split) — behaviour
// unchanged, relocated for organisation only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using System;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    public class DnItemRowViewModel : ViewModelBase
    {
        public event Action? DeliveredQtyChanged;

        public int SpecGroupNumber { get; }
        public string SpecGroupName { get; }
        public int SerialNumber { get; }
        public string GlassRef { get; }
        public int W1 { get; }
        public int H1 { get; }
        public int W2 { get; }
        public int H2 { get; }
        public int OrderedQty { get; }
        public int PreviouslyDeliveredQty { get; }

        private int _deliveredQty;
        public int DeliveredQty
        {
            get => _deliveredQty;
            set
            {
                if (_deliveredQty == value) return;
                int max = Math.Max(0, OrderedQty - PreviouslyDeliveredQty);
                _deliveredQty = Math.Max(0, Math.Min(value, max));
                OnPropertyChanged();
                OnPropertyChanged(nameof(BalanceQty));
                OnPropertyChanged(nameof(HasBalance));
                OnPropertyChanged(nameof(TotalWeight));
                DeliveredQtyChanged?.Invoke();
            }
        }

        public int BalanceQty =>
            Math.Max(0, OrderedQty - PreviouslyDeliveredQty - DeliveredQty);

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes == value) return;
                _notes = value;
                OnPropertyChanged();
            }
        }

        public string DimensionDisplay =>
            W2 > 0 || H2 > 0
                ? $"{W1}×{H1}  /  {W2}×{H2}"
                : $"{W1}×{H1}";

        public bool HasBalance => BalanceQty > 0;
        public bool IsFullyDeliveredLine =>
            BalanceQty <= 0 && PreviouslyDeliveredQty > 0;

        private bool _isSelected;
        /// <summary>True while this row is selected in the slide-in panel.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        public decimal Sqm => SqmCalculator.CalculatePanel(W1, H1);
        public decimal TotalSqm =>
            SqmCalculator.CalculateTotal(W1, H1, OrderedQty);

        // ── Weight tracking ────────────────────────────────────────────────
        // WeightPerPiece is stored on the DeliveryNoteItem model (calculated
        // by DeliveryNoteMapper from glass dimensions + thickness + type).
        // TotalWeight = WeightPerPiece × DeliveredQty (live calculation).
        public decimal WeightPerPiece { get; }

        /// <summary>Live weight for THIS delivery: WeightPerPiece × DeliveredQty.</summary>
        public decimal TotalWeight => WeightPerPiece * DeliveredQty;

        /// <summary>Total weight for ALL ordered pieces (reference).</summary>
        public decimal TotalOrderedWeight => WeightPerPiece * OrderedQty;

        public DnItemRowViewModel(DeliveryNoteItem item)
        {
            SpecGroupNumber = item.SpecGroupNumber;
            SpecGroupName = item.SpecGroupName;
            SerialNumber = item.SerialNumber;
            GlassRef = item.GlassRef;
            W1 = item.W1;
            H1 = item.H1;
            W2 = item.W2;
            H2 = item.H2;
            OrderedQty = item.OrderedQty;
            PreviouslyDeliveredQty = item.PreviouslyDeliveredQty;
            _deliveredQty = item.DeliveredQty;
            _notes = item.Notes;
            WeightPerPiece = item.WeightPerPiece;
        }
    }
}
