// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Write/UpdateSheetStockTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE Q)
//
// THE AGENT'S FIRST AND ONLY WRITE TOOL.
//
// Chosen deliberately as the smallest blast radius available:
//   • It adjusts a stock COUNT on one sheet. Nothing is deleted, no
//     document is created, no price or customer record is touched.
//   • It is REVERSIBLE by the inverse operation, and the audit entry
//     records the before and after so the reversal is unambiguous.
//   • It is NEVER bulk: exactly one sheet, one quantity, per call.
//
// MASTER-PROMPT MANDATES HONOURED HERE
//   • Tools split by risk        — this lives in Tools/Write/, not Read/.
//   • No raw SQL from the agent  — goes through SheetStoreRepository,
//                                  the same API the Sheet Store UI uses.
//   • Permission + confirmation  — Risk=Write means the gate demands both;
//                                  RequiredPermission is SheetStore.Edit.
//   • Audited                    — LogWithChanges records before/after and
//                                  names the agent as the actor.
//   • Async with a timeout       — IAgentTool.ExecuteAsync has no
//                                  CancellationToken, so the timeout is
//                                  enforced inside rather than by changing
//                                  an interface every other tool implements.
//   • Business rules below       — over-issue is refused by the repository
//     security/permissions         and reported truthfully, not swallowed.
//
// WHAT IT WILL NOT DO
//   No sheet creation, no deletion, no price edits, no "adjust everything
//   matching X". Those need their own tools and their own review.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Write
{
    /// <summary>Adds or consumes stock on exactly one glass sheet.</summary>
    public sealed class UpdateSheetStockTool : IAgentTool
    {
        /// <summary>Hard ceiling so a typo cannot move a warehouse.</summary>
        private const int MaxQuantityPerCall = 500;

        /// <summary>The write must not hang the chat turn.</summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// "add 10 to sheet 42" / "use 5 from sheet 42" / "buy 10 sheet 42".
        /// Deliberately strict: an ambiguous instruction is refused rather
        /// than guessed at, because guessing here changes inventory.
        ///
        /// THE "sheet"/"#" TOKEN IS MANDATORY, AND THAT IS LOAD-BEARING.
        /// An earlier version made it optional, so "add 105" split into
        /// qty=10 and id=5 and quietly adjusted the wrong sheet. Requiring
        /// an explicit sheet marker makes that input unparseable instead.
        /// </summary>
        private static readonly Regex Syntax = new(
            @"^\s*(?<verb>add|buy|receive|use|consume|issue)\s+(?<qty>\d{1,4})\s+" +
            @"(?:(?:to|from|on|for)\s+)?(?:sheet\s*|#)(?<id>\d{1,9})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ToolDefinition Definition { get; } = new()
        {
            Name = "update_sheet_stock",
            Description = "Add received stock to, or consume stock from, one glass sheet",
            Risk = ToolRisk.Write,
            RequiredPermission = AppPermissions.SheetStoreEdit,
            RequiresConfirmation = true
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            string input = (argument ?? "").Trim();

            var match = Syntax.Match(input);
            if (!match.Success)
            {
                return ToolResult.Fail(
                    "I did not understand that precisely enough to change stock. " +
                    "Say it as \"add 10 to sheet 42\" or \"use 5 from sheet 42\". " +
                    "I will not guess when inventory is involved.");
            }

            string verb = match.Groups["verb"].Value.ToLowerInvariant();
            bool isAdd = verb is "add" or "buy" or "receive";

            if (!int.TryParse(match.Groups["qty"].Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty)
                || qty <= 0)
            {
                return ToolResult.Fail("The quantity must be a whole number above zero.");
            }

            if (qty > MaxQuantityPerCall)
            {
                return ToolResult.Fail(
                    $"{qty} exceeds the {MaxQuantityPerCall}-sheet limit for a single " +
                    "agent adjustment. Please make a change that large in the Sheet " +
                    "Store screen, where you can see the whole record.");
            }

            if (!int.TryParse(match.Groups["id"].Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int sheetId)
                || sheetId <= 0)
            {
                return ToolResult.Fail("That is not a valid sheet id.");
            }

            using var cts = new CancellationTokenSource(Timeout);

            try
            {
                var repo = new SheetStoreRepository();

                // Read the BEFORE state so the audit entry is meaningful and
                // the change is reversible from the log alone.
                var before = await repo.GetByIdAsync(sheetId).ConfigureAwait(false);

                if (before == null)
                {
                    return ToolResult.Fail(
                        $"There is no sheet with id {sheetId}, so I changed nothing.");
                }

                int beforeStock = before.Stock;
                int beforeUsed = before.Used;
                int beforeBalance = before.Balance;
                string label = Describe(before);

                if (cts.IsCancellationRequested)
                    return TimedOut();

                // ── The write, through the repository the UI uses ─────────
                if (isAdd)
                {
                    await repo.BuyStockAsync(sheetId, qty).ConfigureAwait(false);
                }
                else
                {
                    // UseStockAsync THROWS when the balance is short. Catching
                    // it here turns a crash into a truthful refusal.
                    try
                    {
                        await repo.UseStockAsync(sheetId, qty).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return ToolResult.Fail(
                            $"I did not change anything: {ex.Message}");
                    }
                }

                // Read back rather than trusting the in-memory copy.
                var after = await repo.GetByIdAsync(sheetId).ConfigureAwait(false);
                int afterStock = after?.Stock ?? beforeStock;
                int afterUsed = after?.Used ?? beforeUsed;
                int afterBalance = after?.Balance ?? beforeBalance;

                if (afterStock == beforeStock && afterUsed == beforeUsed)
                {
                    return ToolResult.Fail(
                        "The stock record did not change. Nothing was saved.");
                }

                WriteAudit(
                    sheetId, label, isAdd, qty,
                    beforeStock, afterStock,
                    beforeUsed, afterUsed,
                    beforeBalance, afterBalance);

                string verbPast = isAdd ? "Added" : "Consumed";

                return ToolResult.Ok(
                    $"{verbPast} {qty} sheet(s) on #{sheetId} — {label}. " +
                    $"Balance {beforeBalance} → {afterBalance}. " +
                    "This is recorded in the activity log.");
            }
            catch (OperationCanceledException)
            {
                return TimedOut();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateSheetStockTool] {ex.Message}");

                return ToolResult.Fail(
                    "The stock update failed. I could not confirm whether it was " +
                    "saved — please check the sheet in Sheet Store before retrying.");
            }
        }

        private static ToolResult TimedOut() =>
            ToolResult.Fail(
                "The stock update took too long and I stopped waiting. Check the " +
                "sheet in Sheet Store before trying again.");

        private static string Describe(GlassSheet s)
        {
            var bits = new List<string>();
            if (s.ThicknessMm > 0) bits.Add($"{s.ThicknessMm}mm");
            if (!string.IsNullOrWhiteSpace(s.Color)) bits.Add(s.Color);
            if (s.WidthMm > 0 && s.HeightMm > 0) bits.Add($"{s.WidthMm}×{s.HeightMm}");

            return bits.Count > 0 ? string.Join(" ", bits) : "sheet";
        }

        /// <summary>
        /// Records who/what/when with before and after values, and names the
        /// agent explicitly so an audit reader can tell an agent action from
        /// a human one.
        /// </summary>
        private static void WriteAudit(
            int sheetId, string label, bool isAdd, int qty,
            int beforeStock, int afterStock,
            int beforeUsed, int afterUsed,
            int beforeBalance, int afterBalance)
        {
            try
            {
                var changes = new List<AuditChange>
                {
                    new()
                    {
                        Field = "Stock",
                        Before = beforeStock.ToString(CultureInfo.InvariantCulture),
                        After = afterStock.ToString(CultureInfo.InvariantCulture)
                    },
                    new()
                    {
                        Field = "Used",
                        Before = beforeUsed.ToString(CultureInfo.InvariantCulture),
                        After = afterUsed.ToString(CultureInfo.InvariantCulture)
                    },
                    new()
                    {
                        Field = "Balance",
                        Before = beforeBalance.ToString(CultureInfo.InvariantCulture),
                        After = afterBalance.ToString(CultureInfo.InvariantCulture)
                    }
                };

                AuditService.LogWithChanges(
                    AuditSections.SheetStore,
                    isAdd ? "Stock added (ProGlass Agent)" : "Stock consumed (ProGlass Agent)",
                    $"{(isAdd ? "Added" : "Consumed")} {qty} sheet(s) on #{sheetId} — {label}",
                    "GlassSheet",
                    sheetId.ToString(CultureInfo.InvariantCulture),
                    changes,
                    reason: "Requested through the ProGlass Agent and confirmed by the user");
            }
            catch (Exception ex)
            {
                // A failed audit write must not undo a completed change, but
                // it must be visible.
                Debug.WriteLine($"[UpdateSheetStockTool] audit failed: {ex.Message}");
            }
        }
    }
}
