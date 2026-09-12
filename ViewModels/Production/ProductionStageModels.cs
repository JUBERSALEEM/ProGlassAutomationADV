// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageViewModelBase.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — shared stage view model (piece flow, 2026-09-11)
//
// Base for the eight production stage modules. Production moves piece by
// piece, like a tool chain:
//
//   Cutting → Holes / Polish / Edge Deletion / Mitring → Tempering →
//   Pairing → Doubling / Lamination
//
// Each stage item tracks DONE pieces (QtyDone); only the completed
// quantity arrives in the next module. Every module screen is a
// two-tier master–detail:
//
//   TIER 1 — UNIT LIST (main stage screen): one row per unit with
//            SR · JOB NO · CUSTOMER · TOTAL · DONE · BALANCE plus a
//            status chip. Double-click opens the full-page detail.
//
//   TIER 2 — FULL-PAGE UNIT DETAIL: top completed-KPI strip, a card
//            per pane (Outer / Inner — single glass shows the spec
//            name) with W1×H1 (and W2/H2), per-pane TOTAL/DONE/BALANCE,
//            a Qty Done input + Save, unit totals, and the recent
//            entries log (who marked done, when). Completed pieces flow
//            into the next stage automatically; the main list
//            live-refreshes on every floor entry.
//
// See docs/PRODUCTION_SCREENS_DESIGN_2026-09-11.md for the full design.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// One pane-level stage item in the detail panel with its piece flow
    /// (arrived / done / balance) and the floor's done-qty editor state.
    /// </summary>
    public sealed class ProductionWorkItemRow : ViewModelBase
    {
        private readonly ProductionRepository.ProductionFlowItem _flow;

        public ProductionWorkItemRow(ProductionRepository.ProductionFlowItem flow)
        {
            _flow = flow;
            _doneInput = Done.ToString(CultureInfo.InvariantCulture);
        }

        public SavedProductionWorkItem Item => _flow.Item;
        public int Id => _flow.Item.Id;

        public string Position => string.IsNullOrWhiteSpace(_flow.Item.Position) ? "—" : _flow.Item.Position;
        public string Treatment => string.IsNullOrWhiteSpace(_flow.Item.Treatment) ? "—" : _flow.Item.Treatment;

        public string SizeLabel => _flow.Item.WidthMm > 0 && _flow.Item.HeightMm > 0
            ? $"{_flow.Item.WidthMm.ToString(CultureInfo.InvariantCulture)} × {_flow.Item.HeightMm.ToString(CultureInfo.InvariantCulture)} mm"
            : "—";

        /// <summary>
        /// Card title: OUTER / INNER for pane builds; the spec name
        /// directly for single-glass units (SGU).
        /// </summary>
        public string PaneTitle
        {
            get
            {
                string p = _flow.Item.Position ?? "";
                if (string.Equals(p, "Unit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, "Glass", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(p))
                    return string.IsNullOrWhiteSpace(_flow.Item.SpecName) ? "Glass" : _flow.Item.SpecName;
                return p;
            }
        }

        /// <summary>Spec + treatment + glass ref, only where not already the title.</summary>
        public string PaneSubtitle
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_flow.Item.SpecName) &&
                    !string.Equals(PaneTitle, _flow.Item.SpecName, StringComparison.OrdinalIgnoreCase))
                    parts.Add(_flow.Item.SpecName);
                if (!string.IsNullOrWhiteSpace(_flow.Item.Treatment))
                    parts.Add(_flow.Item.Treatment);
                if (!string.IsNullOrWhiteSpace(_flow.Item.GlassRef))
                    parts.Add("Ref " + _flow.Item.GlassRef);
                return parts.Count == 0 ? "" : string.Join(" · ", parts);
            }
        }

        public string QtyLabel => _flow.Item.Qty > 0
            ? _flow.Item.Qty.ToString(CultureInfo.InvariantCulture) : "—";

        // ── Grid columns (Step-5 cut-list style) ─────────────────────────

        /// <summary>Row number within its grid (1-based, assigned on populate).</summary>
        public int RowIndex { get; set; }

        /// <summary>The unit serial — SR shown in the detail header.</summary>
        public int SerialNumber => _flow.Item.SerialNumber;

        public string GlassRef => string.IsNullOrWhiteSpace(_flow.Item.GlassRef)
            ? "—" : _flow.Item.GlassRef;

        public string WLabel => _flow.Item.WidthMm > 0
            ? _flow.Item.WidthMm.ToString(CultureInfo.InvariantCulture) : "—";

        public string HLabel => _flow.Item.HeightMm > 0
            ? _flow.Item.HeightMm.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>Raw pane position for OUTER/INNER grouping.</summary>
        public string PositionKey => _flow.Item.Position ?? "";

        /// <summary>Fully completed at this stage → moves to the COMPLETED section.</summary>
        public bool IsCompleted => Done >= Inflow && Inflow > 0;

        public string ChargeLabel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_flow.Item.ChargeTemplateKey)) return "";
                string label = _flow.Item.ChargeTemplateKey.Replace('_', ' ');
                if (_flow.Item.ChargeMetricQty > 0)
                {
                    label += $" · {_flow.Item.ChargeMetricQty.ToString("0.###", CultureInfo.InvariantCulture)}" +
                        $" {_flow.Item.ChargeMetricUnit}";
                }
                return label;
            }
        }

        // ── Piece flow ───────────────────────────────────────────────────

        public int Inflow => _flow.Inflow;
        public int Done => _flow.Done;
        public int Balance => _flow.Balance;

        public string InflowLabel => Inflow.ToString(CultureInfo.InvariantCulture);
        public string DoneLabel => Done.ToString(CultureInfo.InvariantCulture);
        public string BalanceLabel => Balance.ToString(CultureInfo.InvariantCulture);

        public string FlowSummaryLabel =>
            $"{Done.ToString(CultureInfo.InvariantCulture)}/{Inflow.ToString(CultureInfo.InvariantCulture)} done";

        public string DetailLabel
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(_flow.Item.PolishSubTypeLabel)) parts.Add(_flow.Item.PolishSubTypeLabel);
                if (!string.IsNullOrWhiteSpace(_flow.Item.Notes)) parts.Add(_flow.Item.Notes);
                return parts.Count == 0 ? "—" : string.Join(" · ", parts);
            }
        }

        public WorkItemStatus Status =>
            Done == 0 ? WorkItemStatus.Pending
            : Done >= Inflow && Inflow > 0 ? WorkItemStatus.Completed
            : WorkItemStatus.InProgress;

        public string StatusLabel => Status switch
        {
            WorkItemStatus.Pending => Inflow == 0 ? "WAITING UPSTREAM" : "PENDING",
            WorkItemStatus.InProgress => "IN PROGRESS",
            WorkItemStatus.Completed => "COMPLETED",
            _ => ""
        };

        public bool CanWork => Inflow > 0;

        /// <summary>Operator who last completed this item (empty if never).</summary>
        public string DoneBy => string.IsNullOrWhiteSpace(_flow.Item.CompletedBy)
            ? "—" : _flow.Item.CompletedBy;

        public string CompletedAtLabel => _flow.Item.CompletedAt == null
            ? "—"
            : _flow.Item.CompletedAt.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

        // ── Done-qty editor ──────────────────────────────────────────────

        private string _doneInput;
        /// <summary>Floor input: pieces completed at this stage.</summary>
        public string DoneInput
        {
            get => _doneInput;
            set { if (_doneInput == value) return; _doneInput = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// One master-list row: a unit with its full Step-5 envelope and this
    /// stage's piece flow (IN / DONE / BALANCE).
    /// </summary>
    public sealed class ProductionListRow
    {
        /// <summary>Unit identity — JobRef|Spec|Serial.</summary>
        public string RowKey { get; init; } = "";

        public int SpecGroupNumber { get; init; }
        public int SerialNumber { get; init; }

        // ── Shared columns ───────────────────────────────────────────────
        public string StatusGlyph { get; init; } = "";
        public string StatusLabel { get; init; } = "";
        public string JobRef { get; init; } = "";
        public string PiNo { get; init; } = "";
        public string Customer { get; init; } = "";
        public string Salesman { get; init; } = "";
        public string SpecLabel { get; init; } = "";
        public string SerialLabel { get; init; } = "—";
        public string GlassRef { get; init; } = "";
        public string BuildType { get; init; } = "";

        // ── Unit envelope — full Step 5 shape, per pane, no pricing ─────
        public string W1Label { get; init; } = "—";
        public string H1Label { get; init; } = "—";
        public string W2Label { get; init; } = "—";
        public string H2Label { get; init; } = "—";
        public string QtyLabel { get; init; } = "—";

        /// <summary>Step 4/5 size rule badge for multi-pane builds.</summary>
        public string SizeRuleLabel { get; init; } = "";
        public string Sqm1Label { get; init; } = "—";
        public string TotalSqm1Label { get; init; } = "—";
        public string Lm1Label { get; init; } = "—";
        public string Sqm2Label { get; init; } = "—";
        public string TotalSqm2Label { get; init; } = "—";
        public string Lm2Label { get; init; } = "—";

        // ── This stage's piece flow ──────────────────────────────────────
        public string InflowLabel { get; init; } = "0";
        public string DoneLabel { get; init; } = "0";
        public string BalanceLabel { get; init; } = "0";
        public string WaitingOn { get; init; } = "";

        /// <summary>This stage's per-item flows (detail panel source).</summary>
        public List<ProductionRepository.ProductionFlowItem> FlowItems { get; init; } = new();
    }

    /// <summary>
    /// One chip in the pipeline chain bar. Every stage module renders the
    /// whole eight-stage chain so the floor can follow work from Cutting
    /// through to Doubling/Lamination; the current stage is highlighted
    /// and any chip navigates to its module.
    /// </summary>
    public sealed class ProductionChainStageChip
    {
        public int Ordinal { get; init; }
        public string NavKey { get; init; } = "";
        public string Name { get; init; } = "";
        public string Icon { get; init; } = "";

        /// <summary>Pieces arrived but not yet done (actionable now).</summary>
        public int Open { get; init; }
        public int Completed { get; init; }
        public int Total { get; init; }

        /// <summary>"pcs" for pane stages, "units" for pairing/assembly.</summary>
        public string UnitLabel { get; init; } = "pcs";

        public bool HasWork => Total > 0;
        public bool IsCleared => Total > 0 && Completed == Total;
        public bool IsCurrent { get; init; }

        public string CountLabel => Open > 0
            ? $"{Open.ToString(CultureInfo.InvariantCulture)} open · " +
              $"{Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)} {UnitLabel}"
            : $"{Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)} {UnitLabel} done";
    }

}
