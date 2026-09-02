// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Reporting/ReportingAnalyticsModels.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 1
//
//  MIX / PARETO ANALYTICS
//  ---------------------------------------------------------------------
//  The existing summaries answer VOLUME ("how many") and FLOW ("what
//  status"). They do not answer MIX: which specifications and which
//  colours actually dominate the work.
//
//  Mix is the analysis that changes purchasing behaviour. "6mm Clear FT
//  is 63% of volume" tells a factory what to hold in stock; "47 job
//  orders" does not. That is why it is the standard third section of any
//  industrial production report.
//
//  DESIGN RULE, non-negotiable:
//  Every aggregate here is computed from THE SAME FILTERED ROW SET that
//  produces the detail table. A KPI band that says 47 while the rows
//  below list 45 destroys trust in the whole document, so these are
//  deliberately pure functions over an already-materialised row list --
//  never a second query with its own WHERE clause that could drift.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// One line in a Pareto ("top N") table: a label, its magnitude, and
    /// its share of the whole.
    /// </summary>
    public class MixRow
    {
        public int Rank { get; set; }

        /// <summary>Specification name, colour, thickness band, etc.</summary>
        public string Label { get; set; } = "";

        /// <summary>Number of documents/rows contributing.</summary>
        public int Count { get; set; }

        /// <summary>Total quantity (pieces).</summary>
        public int Qty { get; set; }

        /// <summary>Total area in square metres — the real volume measure
        /// in glass, since piece count says nothing about size.</summary>
        public decimal Sqm { get; set; }

        /// <summary>Total value where the row carries one.</summary>
        public decimal Value { get; set; }

        /// <summary>Share of the report total, 0-100, by SQM where
        /// available and by count otherwise.</summary>
        public decimal SharePct { get; set; }

        public string SharePctDisplay => $"{SharePct:0.#}%";
        public string SqmDisplay => $"{Sqm:N2}";

        /// <summary>True for the synthetic "Other (n items)" tail row.</summary>
        public bool IsOtherBucket { get; set; }
    }

    /// <summary>
    /// One ageing bucket. Ageing turns an inert number into a call list:
    /// "47 pending" is not actionable, "12 pending over 30 days" is. This
    /// is the most-used view in any dispatch office.
    /// </summary>
    public class AgeingBucket
    {
        public string Label { get; set; } = "";
        public int MinDays { get; set; }
        public int MaxDays { get; set; }

        public int Count { get; set; }
        public int Qty { get; set; }
        public decimal Sqm { get; set; }

        /// <summary>Share of all pending work, 0-100.</summary>
        public decimal SharePct { get; set; }

        public string SharePctDisplay => $"{SharePct:0.#}%";

        /// <summary>
        /// Escalation hint for the UI. The oldest bucket is the one a
        /// dispatcher must act on, so it is flagged rather than left for
        /// the reader to work out.
        /// </summary>
        public bool IsCritical { get; set; }
    }

    /// <summary>
    /// Job Order mix + throughput analytics. Complements JoReportSummary,
    /// which keeps the volume and status figures.
    /// </summary>
    public class JoAnalyticsSummary
    {
        /// <summary>Specifications ranked by SQM, biggest first.</summary>
        public List<MixRow> TopSpecifications { get; set; } = new();

        /// <summary>Glass colours ranked by SQM.</summary>
        public List<MixRow> TopColours { get; set; } = new();

        /// <summary>Glass types (SGU/DGU/LAM/DGU+LAM) ranked by SQM.</summary>
        public List<MixRow> GlassTypeMix { get; set; } = new();

        /// <summary>Customers ranked by value.</summary>
        public List<MixRow> TopCustomers { get; set; } = new();

        /// <summary>Job orders created per calendar week over the range.</summary>
        public List<ThroughputPoint> Throughput { get; set; } = new();

        // ── Headline figures, so a caller never has to re-sum ──────────
        public int TotalJobOrders { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal TotalValue { get; set; }

        /// <summary>Most-used specification, or "—" when there is no data.</summary>
        public string DominantSpecification { get; set; } = "—";

        /// <summary>Most-used colour, or "—".</summary>
        public string DominantColour { get; set; } = "—";

        /// <summary>
        /// Share held by the single largest specification. A high number
        /// means a concentrated product line worth stocking for; a low one
        /// means bespoke work.
        /// </summary>
        public decimal TopSpecSharePct { get; set; }

        public bool HasData => TotalJobOrders > 0;
    }

    /// <summary>One point on a per-week throughput series.</summary>
    public class ThroughputPoint
    {
        public DateTime WeekStart { get; set; }
        public int Count { get; set; }
        public decimal Sqm { get; set; }

        public string Label => WeekStart.ToString("dd MMM");
    }

    /// <summary>
    /// Delivery analytics. DeliveryReportSummary already answers "how many
    /// gone, how many pending"; this answers "how OLD is the pending work,
    /// and who and what is it for" -- the part that drives action.
    /// </summary>
    public class DeliveryAnalyticsSummary
    {
        /// <summary>Pending work bucketed by age.</summary>
        public List<AgeingBucket> PendingAgeing { get; set; } = new();

        /// <summary>Customers ranked by outstanding (pending) quantity.</summary>
        public List<MixRow> PendingByCustomer { get; set; } = new();

        /// <summary>Specifications ranked by outstanding quantity.</summary>
        public List<MixRow> PendingBySpecification { get; set; } = new();

        /// <summary>Delivered volume by specification.</summary>
        public List<MixRow> DeliveredBySpecification { get; set; } = new();

        // ── Headline reconciliation figures ────────────────────────────
        public int TotalRows { get; set; }
        public int OrderedQty { get; set; }
        public int DeliveredQty { get; set; }
        public int PendingQty { get; set; }

        public decimal CompletionPct =>
            OrderedQty <= 0
                ? 0m
                : Math.Round(DeliveredQty / (decimal)OrderedQty * 100m, 1);

        /// <summary>Rows with nothing delivered yet.</summary>
        public int NotStartedRows { get; set; }

        /// <summary>Rows partially delivered — the ones that silently stall.</summary>
        public int PartialRows { get; set; }

        public int CompletedRows { get; set; }

        /// <summary>
        /// Pending quantity older than the critical threshold. This is the
        /// single number a dispatch manager should be shown first.
        /// </summary>
        public int CriticalPendingQty { get; set; }

        public bool HasData => TotalRows > 0;
    }
}
