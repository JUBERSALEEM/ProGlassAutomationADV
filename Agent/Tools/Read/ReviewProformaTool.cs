// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/ReviewProformaTool.cs
// PROJECT: ProGlassAutomation — offline agent reviewer (2026-09-04)
//
// WHAT IT IS:
//   The assistant's "senior checker" for the document the business runs
//   on. Ask the agent:
//
//     review PI-2026-0012
//     check my last proforma
//     any mistakes in my latest PI
//
//   and the agent runs DETERMINISTIC validation over the invoice's own
//   persisted snapshot and reports findings in order of severity. It
//   never invents numbers, never corrects anything itself — it tells the
//   user what to fix and WHERE (which step).
//
// WHY RULE-BASED, NOT "AI GUESSING":
//   A reviewer that cites a rule is auditable; the findings below each
//   cite the exact field and the exact value that triggered them. A
//   hallucinating checker would be worse than no checker.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Rule-based draft reviewer for saved proforma invoices.</summary>
    public sealed class ReviewProformaTool : IAgentTool
    {
        private sealed record Finding(string Severity, string Where, string Text);

        public ToolDefinition Definition { get; } = new()
        {
            Name = "review_proforma",
            Description =
                "Deterministic mistake-check of a saved proforma invoice " +
                "(fields, rows, pricing, VAT readiness) — reads the same " +
                "snapshot the wizard saved, cites the rule and the place " +
                "to fix it, never changes data",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.ProformaView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.Invoices == null)
                    return ReadToolHelpers.Unavailable("proforma invoices");

                var all = await App.Invoices.GetAllAsync().ConfigureAwait(false);

                // Empty arg → the most recently saved one.
                var invoice = term.Length == 0
                    ? all.OrderByDescending(p => p.UpdatedAt).FirstOrDefault()
                    : all.FirstOrDefault(p =>
                        ReadToolHelpers.Hit(p.ProformaId, term) ||
                        ReadToolHelpers.Hit(p.CustomerName, term) ||
                        ReadToolHelpers.Hit(p.ProjectName, term));

                if (invoice == null)
                    return ToolResult.Fail(term.Length == 0
                        ? "No saved proforma invoices exist to review."
                        : $"No proforma invoice matches '{term}'.");

                ProformaInvoiceSnapshot? snap = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(invoice.FullSnapshotJson) &&
                        invoice.FullSnapshotJson != "{}")
                    {
                        snap = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                            invoice.FullSnapshotJson);
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[review_proforma] snapshot unpack: {ex.Message}");
                }

                var findings = new List<Finding>();

                // ── C1 · Identity ───────────────────────────────────────
                if (string.IsNullOrWhiteSpace(invoice.CustomerName))
                    findings.Add(new("HIGH", "Step 2",
                        "Customer name is empty — the invoice has no buyer."));

                if (snap != null)
                {
                    if (string.IsNullOrWhiteSpace(snap.Company.CompanyName))
                        findings.Add(new("HIGH", "Step 1",
                            "Company (seller) name is empty — the invoice " +
                            "would go out with no legal seller entity."));

                    if (snap.Company.CompanyTrn.Trim().Length == 0 && snap.Summary.IsVatApplicable)
                        findings.Add(new("HIGH", "Step 1 + Step 7",
                            "VAT is applied but your company TRN is empty — " +
                            "a UAE tax invoice must carry the seller's TRN."));

                    if (snap.Client.CustomerTrn.Trim().Length == 0)
                        findings.Add(new("MED", "Step 2",
                            "Customer TRN is empty — required for the customer " +
                            "to claim input VAT."));

                    // ── C2 · Spec rows (money geometry) ────────────────
                    int groups = snap.SpecGroups.Count;
                    int rows = snap.SpecGroups.Sum(g => g.ItemRows.Count);

                    if (groups == 0 || rows == 0)
                    {
                        findings.Add(new("HIGH", "Step 5",
                            "No specification / line rows exist — the invoice " +
                            "quotes nothing."));
                    }
                    else
                    {
                        var zeroQty = snap.SpecGroups.SelectMany(g => g.ItemRows)
                            .Where(r => r.Qty <= 0).ToList();
                        if (zeroQty.Count > 0)
                            findings.Add(new("HIGH", "Step 5",
                                $"{zeroQty.Count} row(s) have quantity ≤ 0 " +
                                "(e.g. row {zeroQty[0].SerialNumber}) — they " +
                                "quote zero glass but still occupy paper."));

                        var blankDim = snap.SpecGroups.SelectMany(g => g.ItemRows)
                            .Where(r => r.Qty > 0 && r.W1 == 0 && r.H1 == 0).ToList();
                        if (blankDim.Count > 0)
                            findings.Add(new("HIGH", "Step 5",
                                $"{blankDim.Count} row(s) have zero width AND " +
                                "zero height (e.g. row " +
                                $"{blankDim[0].SerialNumber}) — they cost " +
                                "nothing, which is wrong if they ship."));

                        var unpriced = snap.SpecGroups
                            .Where(g => g.ItemRows.Count > 0 && g.BaseRate <= 0 &&
                                        g.ImportedPrice <= 0)
                            .ToList();
                        if (unpriced.Count > 0)
                            findings.Add(new("HIGH", "Steps 4–5",
                                $"{unpriced.Count} group(s) (e.g. " +
                                $"{unpriced[0].GroupName}) have rows but no " +
                                "price — glass would ship at AED 0.000."));

                        var unnamed = snap.SpecGroups
                            .Where(g => g.ItemRows.Count > 0 &&
                                        string.IsNullOrWhiteSpace(g.SelectedSpecName))
                            .ToList();
                        if (unnamed.Count > 0)
                            findings.Add(new("MED", "Step 5",
                                $"{unnamed.Count} group(s) have rows but no " +
                                "spec name — the factory cannot tell what to " +
                                "produce."));
                    }

                    // ── C3 · Pricing steps skipped ─────────────────────
                    if (rows > 0 && snap.Step4PricingSections.Count == 0 &&
                        snap.SpecGroups.Any(g => g.ImportedPrice <= 0))
                        findings.Add(new("MED", "Step 4",
                            "Rows exist but no pricing-estimator sections were " +
                            "recorded — the P&L step can only report imports, " +
                            "not real costs."));

                    // ── C4 · Summary arithmetic sanity ──────────────────
                    if (snap.Summary.DiscountType == "Percentage" &&
                        snap.Summary.DiscountValue > 50m)
                        findings.Add(new("MED", "Step 7",
                            $"Discount is {snap.Summary.DiscountValue:0.##}% — " +
                            "over half the invoice. Confirm this is intended."));

                    if (snap.Summary.DiscountType == "FixedAmount" &&
                        snap.Summary.DiscountValue > invoice.GrandTotalVatInclusive &&
                        invoice.GrandTotalVatInclusive > 0)
                        findings.Add(new("HIGH", "Step 7",
                            "Fixed discount exceeds the invoice total — the " +
                            "total after discount is negative territory."));

                    if (snap.Summary.IsVatApplicable &&
                        Math.Abs(snap.Summary.VatPercent - 5m) > 0.01m)
                        findings.Add(new("MED", "Step 7",
                            $"VAT is switched on but the rate is " +
                            $"{snap.Summary.VatPercent:0.##}% — UAE VAT is 5%."));

                    if (rows > 0 && invoice.GrandTotalVatInclusive <= 0m)
                        findings.Add(new("HIGH", "Step 7",
                            "Grand total is zero despite spec rows existing — " +
                            "pricing never reached the summary."));

                    if (snap.Company.ValidUntil < snap.Company.IssueDate)
                        findings.Add(new("MED", "Step 1",
                            "Validity date is BEFORE the issue date — the " +
                            "quotation 'expires before it starts'."));

                    if (snap.PreparedBy == null ||
                        string.IsNullOrWhiteSpace(snap.PreparedBy.PreparedByName))
                        findings.Add(new("LOW", "Step 7",
                            "\"Prepared by\" is blank — audit trails and the " +
                            "print layout work best with a name."));
                }
                else
                {
                    findings.Add(new("MED", "—",
                        "The invoice's wizard snapshot could not be opened; " +
                        "only the header fields (customer / total / status) " +
                        "could be checked."));
                }

                // ── Compose the report ─────────────────────────────────
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"📋 Review of {invoice.ProformaId} — {invoice.CustomerName}, " +
                    $"status {invoice.Status}, grand total AED {invoice.GrandTotalVatInclusive:N2}:");

                if (findings.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("✅ No rule violations found by the reviewer. " +
                                  "That is not a guarantee of business quality — " +
                                  "it means every checked field obeys its rule.");
                }
                else
                {
                    foreach (var sev in new[] { "HIGH", "MED", "LOW" })
                    {
                        var group = findings.Where(f => f.Severity == sev).ToList();
                        if (group.Count == 0) continue;
                        sb.AppendLine();
                        sb.AppendLine($"{sev} ({group.Count}):");
                        foreach (var f in group)
                            sb.AppendLine($"  • [{f.Where}] {f.Text}");
                    }
                }

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[review_proforma] {ex.Message}");
                return ReadToolHelpers.Unavailable("proforma invoice review");
            }
        }
    }
}
