// ═══════════════════════════════════════════════════════════════════════
// FILE: ActivityRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: SectionActivityViewModel.cs
//
// ActivityRowViewModel split out of SectionActivityViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProGlassAutomation.Commands;

namespace ProGlassAutomation.ViewModels.Shared
{
    public class ActivityRowViewModel
    {
        public string TimeDisplay { get; set; } = "";
        public string Username { get; set; } = "";
        public string Action { get; set; } = "";
        public string Details { get; set; } = "";
        public string Section { get; set; } = "";

        /// <summary>Raw before/after change set JSON (empty = no diff).</summary>
        public string ChangesJson { get; set; } = "";

        /// <summary>True when a GitHub-style diff can be shown.</summary>
        public bool HasChanges =>
            !string.IsNullOrWhiteSpace(ChangesJson);

        /// <summary>
        /// PHASE D — the WHY the user typed when performing this action.
        /// Empty for entries recorded without a reason.
        /// </summary>
        public string Reason { get; set; } = "";

        /// <summary>True when a WHY note was recorded.</summary>
        public bool HasReason =>
            !string.IsNullOrWhiteSpace(Reason);

        /// <summary>
        /// Full row tooltip: date/time · user · module · action · details.
        /// Shown on hover so every field is discoverable even when the
        /// row is trimmed.
        /// </summary>
        public string RowTooltip
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>
                {
                    $"When:   {TimeDisplay}",
                    $"User:   {Username}",
                    $"Module: {Section}",
                    $"Action: {Action}"
                };
                if (!string.IsNullOrWhiteSpace(Details))
                    parts.Add($"Details: {Details}");
                if (!string.IsNullOrWhiteSpace(Reason))
                    parts.Add($"Why:     {Reason}");
                return string.Join("\n", parts);
            }
        }
    }
}
