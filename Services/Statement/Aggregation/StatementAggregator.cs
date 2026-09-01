// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Aggregation/StatementAggregator.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 22 v3 — Reconciliation)
//
// CHAT 22 v3 CHANGES:
//   NEW:
//     • _reconciliationService field (stateless, one instance per aggregator)
//     • Step 5.5 in BuildAsync — builds reconciliation for all unique PIs
//     • BuildReconciliationMapAsync — batch reconciliation loader
//     • AttachReconciliationToRows — populates row.Reconciliation
//
//   REQUIRES:
//     • StatementInvoiceRow (in StatementSnapshot.cs) must have:
//         public OrderReconciliationSummary? Reconciliation { get; set; }
//     • Namespace ProGlassAutomation.Models.Statement.Reconciliation
//     • Namespace ProGlassAutomation.Services.Statement.Reconciliation
//
// PRESERVED FROM CHAT 20:
//   • All Chat 17 filter fixes (null-safe, salesman multi-path)
//   • Chat 20 project filter JSON search (post-query fallback)
//   • All existing method signatures + bindings
//
// PERFORMANCE:
//   Reconciliation runs AFTER filtering — only for TIs in the final result.
//   Unique PI IDs are deduplicated before loading (batch pattern).
//   For a statement with 100 TIs across 20 PIs → 20 reconciliation loads,
//   not 100. Each PI's reconciliation is computed once and reused for
//   every TI in that family.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Aggregation
{
    /// <summary>
    /// Builds a StatementSnapshot from cross-module data sources.
    /// Single public entry point: BuildAsync().
    /// Called by StatementGeneratorViewModel on a background thread.
    ///
    /// CHAT 22 v3: Now also builds per-line reconciliation showing
    /// Ordered (PI) vs Delivered (DN) vs Invoiced (TI) quantities.
    /// </summary>
    public partial class StatementAggregator
    {
    }
}