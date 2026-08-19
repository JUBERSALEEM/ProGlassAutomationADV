// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Reconciliation/ReconciliationDiagnostics.cs
// PROJECT: ProGlassAutomation
// MODULE: Order Fulfillment Reconciliation (Chat 22 v3 — Debug Helper)
//
// PURPOSE:
//   Diagnostic tool to validate that OrderReconciliationService is
//   computing reconciliation correctly BEFORE we build UI on top.
//   Provides three test entry points:
//
//   1. RunFullDiagnosticsAsync()
//      Iterates through every Tax Invoice family in the DB, runs
//      reconciliation, prints comprehensive report to Output window.
//
//   2. RunSingleProformaAsync(proformaId)
//      Deep-dive one specific PI. Prints PI details, all DNs, all TIs,
//      and the computed reconciliation with per-line breakdown.
//      USE THIS to verify your "ordered=2, delivered=1, invoiced=2" bug.
//
//   3. RunDiscrepancyScanAsync()
//      Fast scan — reports ONLY families with billing discrepancies.
//      Ideal for spotting over-invoicing across your entire database.
//
// HOW TO INVOKE:
//   Option 1 — From App.xaml.cs after initialization:
//     await Services.Statement.Reconciliation.ReconciliationDiagnostics
//         .RunDiscrepancyScanAsync();
//
//   Option 2 — From any command or button click:
//     _ = ReconciliationDiagnostics.RunSingleProformaAsync("PI-2026-001");
//
//   Option 3 — From Immediate Window during debug:
//     await ProGlassAutomation.Services.Statement.Reconciliation
//         .ReconciliationDiagnostics.RunFullDiagnosticsAsync();
//
// OUTPUT LOCATION:
//   All results go to Debug.WriteLine → visible in Visual Studio's
//   Output window (View → Output → Show output from: Debug).
//   Use DebugView++ or similar tool if running outside VS.
//
// ZERO SIDE EFFECTS:
//   Read-only operation. Never writes to database.
//   Safe to run in production for troubleshooting.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Reconciliation
{
    /// <summary>
    /// Diagnostic helper for OrderReconciliationService.
    /// All methods are read-only, safe to run anytime.
    /// Output goes to Debug.WriteLine → Visual Studio Output window.
    /// </summary>
    public static class ReconciliationDiagnostics
    {
        private static readonly OrderReconciliationService _service = new();

        // ═════════════════════════════════════════════════════════════════
        //  TEST 1 — FULL DIAGNOSTICS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs reconciliation on EVERY unique Proforma ID in the Tax
        /// Invoice database. Prints comprehensive report.
        ///
        /// Use this when you first deploy — verifies the service works
        /// against your real data set. May take a while for large DBs.
        /// </summary>
        public static async Task RunFullDiagnosticsAsync()
        {
            PrintHeader("FULL RECONCILIATION DIAGNOSTICS");

            try
            {
                // ── Load all Tax Invoices ─────────────────────────────
                var allInvoices = await App.TaxInvoices.GetAllAsync();

                if (allInvoices == null || allInvoices.Count == 0)
                {
                    PrintLine("❌ No Tax Invoices in database");
                    PrintFooter();
                    return;
                }

                PrintLine($"📊 Total Tax Invoices in DB: {allInvoices.Count}");

                // ── Extract unique Proforma IDs ───────────────────────
                var uniqueProformas = allInvoices
                    .Select(i => i.ProformaId ?? "")
                    .Where(pid => !string.IsNullOrWhiteSpace(pid))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(pid => pid, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                PrintLine($"📋 Unique Proforma families: {uniqueProformas.Count}");
                PrintLine("");

                // ── Run reconciliation for each ───────────────────────
                var summaries = new List<OrderReconciliationSummary>();

                foreach (var proformaId in uniqueProformas)
                {
                    var summary = await _service.BuildForProformaAsync(proformaId);
                    summaries.Add(summary);
                    PrintFamilyLine(summary);
                }

                // ── Aggregate stats ───────────────────────────────────
                PrintLine("");
                PrintSection("SUMMARY STATISTICS");

                int totalItems = summaries.Sum(s => s.Rows.Count);
                int matched = summaries.Sum(s => s.MatchedCount);
                int overInvoiced = summaries.Sum(s => s.OverInvoicedCount);
                int underInvoiced = summaries.Sum(s => s.UnderInvoicedCount);
                int partial = summaries.Sum(s => s.PartialDeliveryCount);
                int notStarted = summaries.Sum(s => s.NotStartedCount);

                PrintLine($"  Total line items analyzed  : {totalItems}");
                PrintLine($"  ✅ Fully matched            : {matched}");
                PrintLine($"  🔴 OVER-INVOICED (CRITICAL) : {overInvoiced}");
                PrintLine($"  🔵 Under-invoiced           : {underInvoiced}");
                PrintLine($"  🟡 Partial delivery         : {partial}");
                PrintLine($"  ⚪ Not yet started          : {notStarted}");

                // ── Highlight critical families ───────────────────────
                var critical = summaries.Where(s => s.HasOverInvoicing).ToList();
                if (critical.Count > 0)
                {
                    PrintLine("");
                    PrintSection($"🔴 CRITICAL — {critical.Count} FAMILIES WITH OVER-INVOICING");

                    foreach (var s in critical)
                    {
                        PrintLine($"  • {s.ProformaId}  →  " +
                                  $"{s.OverInvoicedCount} over-invoiced " +
                                  $"line(s) of {s.Rows.Count} total");
                    }
                    PrintLine("");
                    PrintLine("  ACTION: Run RunSingleProformaAsync(proformaId) on each");
                    PrintLine("          of the above to see line-item details.");
                }
                else
                {
                    PrintLine("");
                    PrintLine("✅ No over-invoicing detected across entire database");
                }
            }
            catch (Exception ex)
            {
                PrintLine($"❌ FATAL ERROR: {ex.Message}");
                PrintLine($"   {ex.GetType().Name}");
                PrintLine($"   {ex.StackTrace}");
            }

            PrintFooter();
        }

        // ═════════════════════════════════════════════════════════════════
        //  TEST 2 — SINGLE PROFORMA DEEP DIVE
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deep-dive analysis of one specific Proforma family.
        /// Prints PI details, all linked DNs, all linked TIs, and the
        /// computed reconciliation with per-line-item breakdown.
        ///
        /// USE THIS FIRST to verify your specific bug scenario.
        /// Example: RunSingleProformaAsync("PI-2026-001")
        /// </summary>
        public static async Task RunSingleProformaAsync(string proformaId)
        {
            PrintHeader($"DEEP DIVE — {proformaId}");

            if (string.IsNullOrWhiteSpace(proformaId))
            {
                PrintLine("❌ Proforma ID is empty");
                PrintFooter();
                return;
            }

            try
            {
                // ── 1. Load PI details ────────────────────────────────
                PrintSection("1. PROFORMA INVOICE (PI)");

                var pi = await App.Invoices.GetByProformaIdAsync(proformaId);
                if (pi == null)
                {
                    PrintLine($"  ❌ PI not found in database");
                    PrintFooter();
                    return;
                }

                PrintLine($"  ProformaId    : {pi.ProformaId}");
                PrintLine($"  Customer      : {pi.CustomerName}");
                PrintLine($"  Project       : {pi.ProjectName}");
                PrintLine($"  Status        : {pi.Status}");
                PrintLine($"  Grand Total   : AED {pi.GrandTotalVatInclusive:N2}");
                PrintLine($"  Issue Date    : {pi.IssueDate:dd MMM yyyy}");

                // ── 2. Load JO(s) ─────────────────────────────────────
                PrintLine("");
                PrintSection("2. JOB ORDER(S)");

                var jobOrders = await App.Jobs.GetBySourceProformaIdAsync(proformaId);
                if (jobOrders.Count == 0)
                {
                    PrintLine($"  ⚠ No Job Orders found for this PI");
                    PrintLine($"    (Direct PI→TI workflow — no delivery tracking)");
                }
                else
                {
                    foreach (var jo in jobOrders)
                    {
                        PrintLine($"  JobOrderId  : {jo.JobOrderId}");
                        PrintLine($"    Status    : {jo.Status}");
                        PrintLine($"    Priority  : {jo.Priority}");
                        PrintLine($"    Delivery  : {jo.DeliveryDate?.ToString("dd MMM yyyy") ?? "—"}");
                    }
                }

                // ── 3. Load DN(s) with delivered qty ──────────────────
                PrintLine("");
                PrintSection("3. DELIVERY NOTES + DELIVERED QUANTITIES");

                var deliveredMap = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var jo in jobOrders)
                {
                    var dns = await App.DeliveryNotes
                        .GetByJobOrderIdAsync(jo.JobOrderId);

                    if (dns.Count == 0)
                    {
                        PrintLine($"  ⚠ No DNs for JO {jo.JobOrderId}");
                        continue;
                    }

                    foreach (var dn in dns)
                    {
                        PrintLine($"  {dn.DeliveryNoteId}  " +
                                  $"({dn.Status})  " +
                                  $"delivered: {dn.TotalDeliveredQty}  " +
                                  $"invoiced: {(dn.HasTaxInvoice ? dn.TaxInvoiceId : "NO")}");
                    }

                    var joMap = await App.DeliveryNotes
                        .GetPreviouslyDeliveredMapAsync(jo.JobOrderId, "");

                    foreach (var kvp in joMap)
                    {
                        if (deliveredMap.ContainsKey(kvp.Key))
                            deliveredMap[kvp.Key] += kvp.Value;
                        else
                            deliveredMap[kvp.Key] = kvp.Value;
                    }
                }

                PrintLine("");
                PrintLine($"  Cumulative delivered map ({deliveredMap.Count} entries):");
                foreach (var kvp in deliveredMap.OrderBy(k => k.Key))
                {
                    PrintLine($"    spec|serial={kvp.Key,-15}  qty={kvp.Value}");
                }

                // ── 4. Load TI(s) with invoiced qty ───────────────────
                PrintLine("");
                PrintSection("4. TAX INVOICES + INVOICED QUANTITIES");

                var tis = await App.TaxInvoices.GetByProformaIdAsync(proformaId);
                if (tis.Count == 0)
                {
                    PrintLine($"  ⚠ No Tax Invoices found");
                }
                else
                {
                    foreach (var ti in tis)
                    {
                        PrintLine($"  {ti.InvoiceNumber}  " +
                                  $"({ti.Status})  " +
                                  $"qty: {ti.TotalQty}  " +
                                  $"grand: AED {ti.GrandTotal:N2}");
                    }
                }

                // ── 5. Run reconciliation ─────────────────────────────
                PrintLine("");
                PrintSection("5. RECONCILIATION RESULT");

                var summary = await _service.BuildForProformaAsync(proformaId);

                PrintLine($"  Overall Status : {summary.OverallIcon} {summary.OverallStatus.ToLabel()}");
                PrintLine($"  Summary        : {summary.SummaryDisplay}");
                PrintLine($"  Totals         : {summary.TotalsDisplay}");
                PrintLine("");
                PrintLine($"  HasProformaData     : {summary.HasProformaData}");
                PrintLine($"  HasDeliveryTracking : {summary.HasDeliveryTracking}");
                PrintLine($"  HasDiscrepancies    : {summary.HasDiscrepancies}");
                PrintLine($"  HasOverInvoicing    : {summary.HasOverInvoicing}  {(summary.HasOverInvoicing ? "🔴 CRITICAL" : "")}");
                PrintLine($"  HasUnderInvoicing   : {summary.HasUnderInvoicing}");
                PrintLine($"  HasPendingDelivery  : {summary.HasPendingDelivery}");

                // ── 6. Per-line breakdown ─────────────────────────────
                PrintLine("");
                PrintSection("6. LINE-ITEM BREAKDOWN");

                if (summary.Rows.Count == 0)
                {
                    PrintLine("  ⚠ No line items in reconciliation");
                }
                else
                {
                    // Table header
                    PrintLine("  " +
                        "Ref".PadRight(12) +
                        "Spec".PadRight(6) +
                        "S/N".PadRight(5) +
                        "Ordered".PadRight(9) +
                        "Delivered".PadRight(11) +
                        "Balance".PadRight(9) +
                        "Invoiced".PadRight(10) +
                        "Diff".PadRight(6) +
                        "Status");
                    PrintLine("  " + new string('─', 100));

                    foreach (var row in summary.Rows.OrderBy(r => r.SpecGroupNumber)
                                                    .ThenBy(r => r.SerialNumber))
                    {
                        string diffText = row.InvoiceVsDeliveryDiff switch
                        {
                            > 0 => $"+{row.InvoiceVsDeliveryDiff}",
                            < 0 => $"{row.InvoiceVsDeliveryDiff}",
                            _ => "0"
                        };

                        PrintLine("  " +
                            row.GlassRef.PadRight(12) +
                            row.SpecGroupNumber.ToString().PadRight(6) +
                            row.SerialNumber.ToString().PadRight(5) +
                            row.OrderedQty.ToString().PadRight(9) +
                            row.DeliveredQty.ToString().PadRight(11) +
                            row.BalanceQty.ToString().PadRight(9) +
                            row.InvoicedQty.ToString().PadRight(10) +
                            diffText.PadRight(6) +
                            row.StatusIcon + " " + row.StatusLabel);
                    }
                }

                // ── 7. Action recommendations ─────────────────────────
                if (summary.HasDiscrepancies)
                {
                    PrintLine("");
                    PrintSection("7. RECOMMENDED ACTIONS");

                    foreach (var row in summary.Rows.Where(r =>
                        r.Status != ReconciliationStatus.FullyMatched &&
                        r.Status != ReconciliationStatus.NotYetStarted))
                    {
                        PrintLine($"  {row.StatusIcon} {row.GlassRef} " +
                                  $"(Spec {row.SpecGroupNumber}, Row {row.SerialNumber})");
                        PrintLine($"     → {row.ActionLabel}");
                    }
                }
            }
            catch (Exception ex)
            {
                PrintLine($"❌ FATAL ERROR: {ex.Message}");
                PrintLine($"   {ex.GetType().Name}");
                PrintLine($"   {ex.StackTrace}");
            }

            PrintFooter();
        }

        // ═════════════════════════════════════════════════════════════════
        //  TEST 3 — DISCREPANCY SCAN (FAST)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans entire database, reports ONLY families with discrepancies.
        /// Fast — skips fully-matched families to reduce noise.
        ///
        /// Use this regularly as a "health check" — no news is good news.
        /// </summary>
        public static async Task RunDiscrepancyScanAsync()
        {
            PrintHeader("DISCREPANCY SCAN");

            try
            {
                var allInvoices = await App.TaxInvoices.GetAllAsync();

                if (allInvoices == null || allInvoices.Count == 0)
                {
                    PrintLine("❌ No Tax Invoices in database");
                    PrintFooter();
                    return;
                }

                var uniqueProformas = allInvoices
                    .Select(i => i.ProformaId ?? "")
                    .Where(pid => !string.IsNullOrWhiteSpace(pid))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                PrintLine($"Scanning {uniqueProformas.Count} Proforma families...");
                PrintLine("");

                var issues = new List<OrderReconciliationSummary>();

                foreach (var pid in uniqueProformas)
                {
                    var summary = await _service.BuildForProformaAsync(pid);
                    if (summary.HasDiscrepancies)
                        issues.Add(summary);
                }

                if (issues.Count == 0)
                {
                    PrintLine("✅ ALL CLEAN — no discrepancies found across " +
                              $"{uniqueProformas.Count} families");
                }
                else
                {
                    PrintLine($"⚠ Found {issues.Count} families with discrepancies:");
                    PrintLine("");

                    // Sort: over-invoiced first (most critical)
                    var sorted = issues
                        .OrderByDescending(s => s.HasOverInvoicing)
                        .ThenByDescending(s => s.OverInvoicedCount)
                        .ThenBy(s => s.ProformaId)
                        .ToList();

                    foreach (var s in sorted)
                    {
                        PrintLine($"  {s.OverallIcon} {s.ProformaId}");
                        PrintLine($"     {s.SummaryDisplay}");
                        PrintLine($"     {s.TotalsDisplay}");
                        PrintLine("");
                    }

                    PrintLine("");
                    PrintLine("To investigate a specific family, run:");
                    PrintLine("  ReconciliationDiagnostics.RunSingleProformaAsync(\"PI-XXXX-XXX\")");
                }
            }
            catch (Exception ex)
            {
                PrintLine($"❌ FATAL ERROR: {ex.Message}");
                PrintLine($"   {ex.StackTrace}");
            }

            PrintFooter();
        }

        // ═════════════════════════════════════════════════════════════════
        //  OUTPUT FORMATTING HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static void PrintHeader(string title)
        {
            AppLogService.Debug("");
            AppLogService.Debug("╔══════════════════════════════════════════════════════════════════════╗");
            AppLogService.Debug($"║  {title.PadRight(66)}  ║");
            AppLogService.Debug($"║  Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}".PadRight(70) + "  ║");
            AppLogService.Debug("╚══════════════════════════════════════════════════════════════════════╝");
        }

        private static void PrintFooter()
        {
            AppLogService.Debug("");
            AppLogService.Debug("═══════════════════════════════════════════════════════════════════════");
            AppLogService.Debug($"Diagnostics complete at {DateTime.Now:HH:mm:ss}");
            AppLogService.Debug("═══════════════════════════════════════════════════════════════════════");
            AppLogService.Debug("");
        }

        private static void PrintSection(string title)
        {
            AppLogService.Debug("");
            AppLogService.Debug($"── {title} ".PadRight(72, '─'));
        }

        private static void PrintLine(string text)
        {
            AppLogService.Debug(text);
        }

        private static void PrintFamilyLine(OrderReconciliationSummary s)
        {
            string statusIcon = s.OverallIcon;
            string flag = s.HasOverInvoicing ? " 🔴" : "";

            AppLogService.Debug($"  {statusIcon} {s.ProformaId,-20} " +
                            $"O:{s.TotalOrderedQty,3}  " +
                            $"D:{s.TotalDeliveredQty,3}  " +
                            $"I:{s.TotalInvoicedQty,3}  " +
                            $"B:{s.TotalBalanceQty,3}  " +
                            $"→ {s.SummaryDisplay}{flag}");
        }
    }
}