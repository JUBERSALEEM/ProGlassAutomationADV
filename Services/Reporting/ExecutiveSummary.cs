// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Reporting/ExecutiveSummary.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 5
//
//  ONE-PAGE CROSS-MODULE VIEW
//  ---------------------------------------------------------------------
//  Every existing report answers one module. Nobody running the business
//  asks "how are job orders?" in isolation — they ask "where is work
//  getting stuck between quoting and getting paid?"
//
//  That question is the ORDER-TO-CASH FUNNEL, the standard executive view
//  in manufacturing ERP:
//
//      Proforma  →  Job Order  →  Delivery  →  Tax Invoice  →  Paid
//
//  The value is in the CONVERSION RATES between stages, not the counts.
//  A stage that drops sharply is where the business is losing money, and
//  it is invisible in any single-module report.
//
//  SINCE 2026-09-10 every stage counts QUOTES FROM THE SAME COHORT that
//  reached the milestone (became a job order → fully delivered → invoiced
//  → paid). The earlier per-stage document counts mixed units — delivery
//  spec rows outnumbered quotes and "conversion" exceeded 100% — so the
//  percentages were mathematically meaningless. See
//  docs/EXECUTIVE_FUNNEL_FIX.md.
//
//  All figures come from the existing per-module summaries under ONE
//  shared filter, so the executive page and the module reports can never
//  disagree — the cardinal rule from slice 1 applied across modules.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>One stage of the order-to-cash funnel.</summary>
    public class FunnelStage
    {
        public string Label { get; set; } = "";

        /// <summary>Documents at this stage.</summary>
        public int Count { get; set; }

        /// <summary>Value at this stage, where the stage carries one.</summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Share of the FIRST stage, 0-100. Using the first stage as the
        /// denominator (rather than the previous one) makes the whole
        /// funnel readable as a single drop-off curve.
        /// </summary>
        public decimal SharePct { get; set; }

        /// <summary>
        /// Conversion from the immediately preceding stage, 0-100. This is
        /// the number that localises a problem: 90% overall but 40% at one
        /// step means that step is the bottleneck.
        /// </summary>
        public decimal StepConversionPct { get; set; }

        public string CountDisplay => Count.ToString("N0");
        public string ValueDisplay => Value.ToString("N2");
        public string SharePctDisplay => $"{SharePct:0.#}%";
        public string StepConversionDisplay => $"{StepConversionPct:0.#}%";

        /// <summary>
        /// Size of the cohort every stage is measured against (the first
        /// stage's count). Milestone rows render "x of N" instead of a bare
        /// count, which is what makes a cohort funnel readable. 0 = cohort
        /// size unknown.
        /// </summary>
        public int FirstStageCount { get; set; }

        /// <summary>
        /// "x of N" for milestone stages; falls back to the plain count for
        /// the first stage (where x IS N) and when the cohort size is unset.
        /// </summary>
        public string CountOfDisplay =>
            FirstStageCount > 0 && Count != FirstStageCount
                ? $"{Count:N0} of {FirstStageCount:N0}"
                : CountDisplay;

        /// <summary>
        /// Flags an unusually large drop so the reader does not have to
        /// scan for it. Threshold is deliberately generous — a warning that
        /// fires on every row is ignored.
        /// </summary>
        public bool IsBottleneck { get; set; }
    }

    /// <summary>
    /// Cross-module executive summary for one filter period.
    /// </summary>
    public class ExecutiveSummary
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>Human-readable description of the filter in force.</summary>
        public string PeriodDescription { get; set; } = "All time";

        // ── Funnel ─────────────────────────────────────────────────────
        public List<FunnelStage> Funnel { get; set; } = new();

        // ── Headline money ─────────────────────────────────────────────
        public decimal QuotedValue { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal OutstandingValue { get; set; }

        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;

        public decimal MarginPct =>
            Revenue == 0m ? 0m : Math.Round(Profit / Revenue * 100m, 1);

        // ── Operational health ─────────────────────────────────────────

        /// <summary>Quotes that never became job orders.</summary>
        public int UnconvertedQuotes { get; set; }

        /// <summary>Confirmed quotes with no tax invoice raised yet.</summary>
        public int UninvoicedConfirmed { get; set; }

        /// <summary>Invoices past their due date.</summary>
        public int OverdueInvoices { get; set; }

        /// <summary>Delivery completion across the period, 0-100.</summary>
        public decimal DeliveryCompletionPct { get; set; }

        /// <summary>Pending pieces older than the critical threshold.</summary>
        public int CriticalPendingQty { get; set; }

        // ── Mix, reused from slice 1 ───────────────────────────────────
        public List<MixRow> TopSpecifications { get; set; } = new();
        public List<MixRow> TopCustomers { get; set; } = new();

        public string DominantSpecification { get; set; } = "—";
        public string DominantColour { get; set; } = "—";

        /// <summary>
        /// Costing coverage. A margin computed from half the invoices is
        /// worse than no margin, because it looks authoritative — so the
        /// gap is surfaced rather than buried.
        /// </summary>
        public int InvoicesWithoutCost { get; set; }

        public bool HasCostGap => InvoicesWithoutCost > 0;

        public bool HasData => Funnel.Count > 0 && Funnel[0].Count > 0;
    }
}
