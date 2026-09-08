// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/OrderReport/OrderReportSpecGroups.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — Step 5 Specifications breakdown (2026-09-06)
//
// PURPOSE:
//   Read-only, computed view models for the Step 5 "Specifications" screen.
//   They mirror the Proforma Invoice Step-5 data-grid pattern: one block per
//   Proforma ("PI 1"), inside it one block per specification group
//   ("Spec 1 — specification"), each with its row data and a footer total
//   that sits in the SAME column grid so the sums land exactly under their
//   columns.
//
//   These are NOT persisted (unlike OrderReportSnapshot) — they are rebuilt
//   on demand from an OrderReportBundle by OrderReportGeneratorViewModel.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Models.OrderReport
{
    /// <summary>
    /// "PI 1" grouping — one Proforma Invoice with all of its spec groups
    /// and a grand-total row.
    /// </summary>
    public class OrderReportSpecPiGroup
    {
        /// <summary>1-based position in the bundle ("PI 1", "PI 2"…).</summary>
        public int Index { get; set; }

        public string ProformaId { get; set; } = "";
        public string PiStatus { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public DateTime IssueDate { get; set; }

        public List<OrderReportSpecGroup> Groups { get; set; } = new();

        // ── Grand totals (sum of the group footers) ──────────────────────
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        public int GroupCount => Groups.Count;
        public bool HasMultipleGroups => Groups.Count > 1;

        public string TitleDisplay => $"PI {Index} · {ProformaId}";
        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");

        public string TotalQtyDisplay => TotalQty.ToString("N0");
        public string TotalSqmDisplay => TotalSqm.ToString("N3");
        public string TotalValueDisplay => $"AED {TotalValue:N2}";
    }

    /// <summary>
    /// "Spec N — specification" grouping within one Proforma, plus its
    /// footer totals (aligned under the Qty / SQM / Amount columns).
    /// </summary>
    public class OrderReportSpecGroup
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string GlassType { get; set; } = "";

        public List<OrderReportLineItem> Rows { get; set; } = new();

        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        public int RowCount => Rows.Count;

        /// <summary>Header text: "Spec 1 — 6mm Clear Float".</summary>
        public string SpecDisplayName =>
            $"Spec {GroupNumber} — {SpecificationText}";

        public string SpecificationText =>
            !string.IsNullOrWhiteSpace(SpecName) ? SpecName.Trim()
            : !string.IsNullOrWhiteSpace(GroupName) ? GroupName.Trim()
            : !string.IsNullOrWhiteSpace(GlassType) ? GlassType.Trim()
            : "Specification";

        public string TotalQtyDisplay => TotalQty.ToString("N0");
        public string TotalSqmDisplay => TotalSqm.ToString("N3");
        public string TotalValueDisplay => $"AED {TotalValue:N2}";
    }

    /// <summary>
    /// Flat export row for the Step 5 specifications table (CSV / Excel /
    /// generic PDF). Carries the PI context so exported data is traceable.
    /// </summary>
    public class OrderReportSpecExportRow
    {
        public string ProformaId { get; set; } = "";
        public int GroupNumber { get; set; }
        public string Specification { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public string GlassType { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int Qty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public decimal WeightKg { get; set; }

        public string Dimensions => W1 > 0 || H1 > 0
            ? ((W2 <= 0 || W2 == W1) && (H2 <= 0 || H2 == H1)
                ? $"{W1}x{H1}"
                : $"{W1}x{H1} / {W2}x{H2}")
            : "—";

        public static string[] Headers => new[]
        {
            "PI #", "Spec", "Specification", "Serial", "Glass Ref", "Type",
            "Dimensions", "Qty", "SQM", "Rate", "Amount",
            "Delivered", "Balance", "Weight (kg)"
        };

        public string[] ToCsvRow() => new[]
        {
            ProformaId, GroupNumber.ToString(), Specification, SerialNumber.ToString(),
            GlassRef, GlassType, Dimensions, Qty.ToString(),
            TotalSqm.ToString("N3", System.Globalization.CultureInfo.InvariantCulture),
            Rate.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
            Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
            DeliveredQty.ToString(), BalanceQty.ToString(),
            WeightKg.ToString("N1", System.Globalization.CultureInfo.InvariantCulture)
        };

        public object[] ToExcelRow() => new object[]
        {
            ProformaId, GroupNumber, Specification, SerialNumber,
            GlassRef, GlassType, Dimensions, Qty, TotalSqm, Rate, Amount,
            DeliveredQty, BalanceQty, WeightKg
        };
    }

    /// <summary>Flat sectioned export row for Step 9 data inside Report Generator.</summary>
    public class OrderReportStep9ExportRow
    {
        public string Section { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string Spec { get; set; } = "";
        public string Item { get; set; } = "";
        public string Basis { get; set; } = "";
        public string SheetOrTarget { get; set; } = "";
        /// <summary>
        /// Optional monetary value for this export line. Detail/audit rows
        /// intentionally remain blank instead of printing a misleading AED 0.
        /// </summary>
        public decimal? Amount { get; set; }
        public string Detail { get; set; } = "";

        public static string[] Headers => new[]
        {
            "SECTION", "PI #", "SPEC", "ITEM", "BASIS", "SHEET / TARGET", "AMOUNT / VALUE", "DETAIL"
        };

        public string[] ToCsvRow() => new[]
        {
            Section, ProformaId, Spec, Item, Basis, SheetOrTarget,
            Amount.HasValue
                ? Amount.Value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)
                : "—", Detail
        };

        public object[] ToExcelRow() => new object[]
        {
            Section, ProformaId, Spec, Item, Basis, SheetOrTarget,
            Amount.HasValue ? (object)Amount.Value : "", Detail
        };
    }

}
