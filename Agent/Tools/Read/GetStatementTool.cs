// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetStatementTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §9 (statement awareness), §4 (reuse, do not build a second
//                accounting engine), §12 (never invent accounting rules),
//                §21 (read-only), §29/§30 (confidence, no fabrication),
//                §33 (cross-module: who is outstanding?)
//
// ANSWERS: "Show the statement for ABC Glass."
//          "What is their outstanding amount?"
//          "Which customers have outstanding statements?"
//
// ─────────────────────────────────────────────────────────────────────
//  NO SECOND ACCOUNTING ENGINE
// ─────────────────────────────────────────────────────────────────────
//  ProGlass already computes TotalInvoiced, TotalOutstanding and
//  OverdueAmount inside StatementAggregator, and persists them on
//  StatementListItem. This tool READS those saved figures. It does no
//  arithmetic of its own beyond summing rows the engine already produced,
//  so it cannot disagree with the Statement module (§4, §12).
//
//  It deliberately does NOT call StatementAggregator.BuildAsync: that
//  builds a NEW statement and allocates a statement number, which is a
//  write dressed as a query. A read tool must never mint a document.
//
// ─────────────────────────────────────────────────────────────────────
//  PERMISSION CHOICE — DELIBERATE
// ─────────────────────────────────────────────────────────────────────
//  The only statement permission in this app is Statement.Generate, which
//  is a WRITE right (create a statement). Requiring it for a read would
//  deny users who may legitimately view figures, and would conflate
//  "may look" with "may produce".
//
//  Statements are derived from tax invoices, so TaxInvoice.View is the
//  honest read gate: anyone allowed to see the invoices is allowed to see
//  their totals.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Repository;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Reads saved customer statements. Never generates one.</summary>
    public sealed class GetStatementTool : IAgentTool
    {
        private const int MaxRows = 8;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_statement",
            Description =
                "Saved customer statements: invoiced, outstanding and overdue totals",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.TaxInvoiceView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string term = (argument ?? "").Trim();

            try
            {
                if (App.Statements == null)
                    return ReadToolHelpers.Unavailable("statements");

                List<StatementListItem> rows;

                if (term.Length == 0)
                {
                    // No customer named → the portfolio view (§33).
                    rows = await App.Statements.GetAllAsync().ConfigureAwait(false)
                           ?? new List<StatementListItem>();
                }
                else
                {
                    // Customer first, then company, then project. Each is a
                    // "contains" match in the repository, so a partial name
                    // works the way a person would expect.
                    rows = await App.Statements.GetByCustomerAsync(term)
                        .ConfigureAwait(false) ?? new List<StatementListItem>();

                    if (rows.Count == 0)
                        rows = await App.Statements.GetByCompanyAsync(term)
                            .ConfigureAwait(false) ?? new List<StatementListItem>();

                    if (rows.Count == 0)
                        rows = await App.Statements.GetByProjectAsync(term)
                            .ConfigureAwait(false) ?? new List<StatementListItem>();
                }

                if (rows.Count == 0)
                {
                    return ToolResult.Ok(term.Length == 0
                        ? "There are no saved statements yet."
                        : $"No saved statement matches '{term}'. It may not have " +
                          "been generated yet — I only read statements that exist, " +
                          "I do not create them.");
                }

                var sb = new StringBuilder();

                sb.AppendLine(term.Length == 0
                    ? $"Saved statements ({rows.Count}):"
                    : $"Statements matching '{term}' ({rows.Count}):");

                foreach (var r in rows.Take(MaxRows))
                {
                    string who = FirstNonEmpty(
                        r.FilterCustomer, r.FilterCompany, r.FilterProject,
                        r.Title, r.StatementNumber);

                    sb.AppendLine(
                        $"• {r.StatementNumber} — {who} — " +
                        $"{r.InvoiceCount} invoice(s) — " +
                        $"invoiced AED {r.TotalInvoiced:N2} — " +
                        $"outstanding AED {r.TotalOutstanding:N2}" +
                        (r.OverdueAmount > 0m
                            ? $" — OVERDUE AED {r.OverdueAmount:N2}"
                            : ""));
                }

                if (rows.Count > MaxRows)
                    sb.AppendLine($"  (+{rows.Count - MaxRows} more)");

                // ── Portfolio roll-up ─────────────────────────────────────
                decimal outstanding = rows.Sum(r => r.TotalOutstanding);
                decimal overdue = rows.Sum(r => r.OverdueAmount);

                sb.AppendLine();
                sb.AppendLine(
                    $"Total outstanding across these: AED {outstanding:N2}" +
                    (overdue > 0m ? $", of which AED {overdue:N2} is overdue." : "."));

                // ── Honesty about what these numbers are (§30) ────────────
                sb.AppendLine(
                    "These are the figures stored when each statement was " +
                    "generated, not a live recalculation — regenerate the " +
                    "statement if invoices have changed since.");

                var provenance = AnswerProvenance.Calculated(
                    "StatementRepository (figures produced by StatementAggregator)",
                    "sum of stored TotalOutstanding and OverdueAmount",
                    rows.Take(MaxRows).Select(r => r.StatementNumber).ToArray());

                string qualifier = provenance.UserQualifier();
                if (qualifier.Length > 0) sb.AppendLine().Append(qualifier);

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetStatementTool] {ex.Message}");
                return ReadToolHelpers.Unavailable("statements");
            }
        }

        private static string FirstNonEmpty(params string[] candidates)
        {
            foreach (var c in candidates)
                if (!string.IsNullOrWhiteSpace(c)) return c.Trim();

            return "unnamed";
        }
    }
}
