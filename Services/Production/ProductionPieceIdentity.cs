// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionPieceIdentity.cs
// MODULE: Production — piece identity contract (no RFID, no second engine)
//
// WorkItem.UniqueKey  = JOB|SPEC|SERIAL|POSITION|STAGE  (± CHG / RPL)
//   Qty = finished-unit or pane PCS at that stage — not one physical lite.
//
// Pane instance        = CuttingRequirementService.BuildPieceId
//   JOB|SPEC|SERIAL|POSITION|WxH|P0001  (± |RPL:n)
//   One identity per physical glass lite at a position. Survives stage
//   moves. ReplacementSequence > 0 must never collide with the original.
//
// Finished unit        = PairingId / AssemblyId (JobRef|Spec|Serial|Pair:n)
//   DGU 17 units = 17 pairs, not 34 panes.
//
// ProcessExecutionId, ReservationId, AllocationId, CuttingSessionId
// are correlation / planning keys — never PieceId.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Text.RegularExpressions;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production.Cutting;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionPieceIdentity
    {
        private static readonly Regex PaneTail = new(
            @"\|P\d{4}(?:\|RPL:\d+)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Canonical pane-instance id. Index is 1-based (P0001). Replacement
        /// generations append |RPL:n so they cannot masquerade as the original.
        /// </summary>
        public static string BuildPaneInstanceId(
            string jobRef, int specNo, int serial, string position,
            int widthMm, int heightMm, int pieceIndex1Based,
            int replacementSequence = 0)
        {
            string id = CuttingRequirementService.BuildPieceId(
                jobRef, specNo, serial, position, widthMm, heightMm, pieceIndex1Based);
            if (replacementSequence > 0)
                id += "|RPL:" + replacementSequence.ToString();
            return id;
        }

        public static bool IsPaneInstanceId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return PaneTail.IsMatch(id.Trim());
        }

        public static bool IsReplacementWorkItemKey(string? uniqueKey) =>
            !string.IsNullOrWhiteSpace(uniqueKey)
            && uniqueKey.IndexOf("|RPL:", StringComparison.Ordinal) >= 0;

        public static bool IsCuttingSessionId(string? id) =>
            !string.IsNullOrWhiteSpace(id)
            && id.StartsWith("CUT-", StringComparison.Ordinal);

        public static bool LooksLikeExecutionId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || IsPaneInstanceId(id)) return false;
            var parts = id.Split('|');
            // JobRef|Spec|Serial|ProcessType|Sequence — five numeric-tailed tokens, no position.
            return parts.Length == 5
                && int.TryParse(parts[1], out _)
                && int.TryParse(parts[2], out _)
                && int.TryParse(parts[3], out int process)
                && process is >= 1 and <= 8
                && int.TryParse(parts[4], out _);
        }

        public static string Classify(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "none";
            string s = id.Trim();
            if (IsPaneInstanceId(s))
                return s.IndexOf("|RPL:", StringComparison.Ordinal) >= 0
                    ? "pane-instance-replacement"
                    : "pane-instance";
            if (IsCuttingSessionId(s)) return "cutting-session";
            if (s.IndexOf("|Pair:", StringComparison.Ordinal) >= 0) return "pairing";
            if (s.IndexOf("|DGU:", StringComparison.Ordinal) >= 0
                || s.IndexOf("|LAM:", StringComparison.Ordinal) >= 0
                || s.IndexOf("|DGU+LAM:", StringComparison.Ordinal) >= 0)
                return "assembly";
            if (LooksLikeExecutionId(s)) return "process-execution";
            if (IsReplacementWorkItemKey(s)) return "workitem-replacement";
            return "workitem-or-other";
        }

        public static string FromWorkItem(
            SavedProductionWorkItem wi, int pieceIndex1Based)
        {
            if (wi == null) return string.Empty;
            return BuildPaneInstanceId(
                wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Position,
                wi.WidthMm, wi.HeightMm, pieceIndex1Based, wi.ReplacementSequence);
        }
    }
}
