// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Teaching/AgentTeachingService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// "Whenever I say X, do Y."
//
// THE SAFETY PROPERTY: a proposed rule is INERT. Detect() is only ever
// given ActiveRulesAsync() (approved rules), so teaching the agent
// something never grants it a new behaviour until a human approves that
// specific rule. Approve, revoke and clear are all audited.
//
// Actions are restricted to the action DSL produced by
// IntentDetector.MapAction — "navigate:<target>" or "tool:<name>[:<q>]".
// A rule can therefore never express anything the agent could not already
// do; it only creates a new PHRASE for an existing capability.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Teaching
{
    /// <summary>SQLite-backed teaching rules with an approval gate.</summary>
    public sealed class AgentTeachingService : IAgentTeachingService
    {
        private readonly IAgentAuditService? _audit;

        public AgentTeachingService(IAgentAuditService? audit = null)
        {
            _audit = audit;
            AgentDbContext.EnsureCreated();
        }

        /// <summary>Stores an UNAPPROVED rule. Returns 0 on failure.</summary>
        public async Task<int> ProposeAsync(string trigger, string action)
        {
            trigger = (trigger ?? "").Trim();
            action = (action ?? "").Trim();

            if (trigger.Length == 0 || action.Length == 0) return 0;

            // Only the known action DSL is storable.
            if (!action.StartsWith("navigate:", StringComparison.OrdinalIgnoreCase) &&
                !action.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
                return 0;

            try
            {
                using var db = new AgentDbContext();

                var rule = new TeachingRule
                {
                    Trigger = Cap(trigger, 300),
                    Action = Cap(action, 300),
                    IsApproved = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.TeachingRules.Add(rule);
                await db.SaveChangesAsync();

                _audit?.Teaching("", "Rule proposed",
                    $"#{rule.Id} '{rule.Trigger}' → {rule.Action} (awaiting approval)");

                return rule.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] Propose failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Approves a rule so the matcher may use it.</summary>
        public async Task<bool> ApproveAsync(int id, string approvedBy)
        {
            try
            {
                using var db = new AgentDbContext();
                var rule = await db.TeachingRules.FindAsync(id);
                if (rule == null) return false;

                rule.IsApproved = true;
                rule.ApprovedBy = Cap(approvedBy ?? "", 60);
                await db.SaveChangesAsync();

                _audit?.Teaching(approvedBy ?? "", "Rule approved",
                    $"#{rule.Id} '{rule.Trigger}' → {rule.Action}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] Approve failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Deletes a rule outright (revoking is not a soft flag).</summary>
        public async Task<bool> RevokeAsync(int id)
        {
            try
            {
                using var db = new AgentDbContext();
                var rule = await db.TeachingRules.FindAsync(id);
                if (rule == null) return false;

                db.TeachingRules.Remove(rule);
                await db.SaveChangesAsync();

                _audit?.Teaching("", "Rule revoked",
                    $"#{id} '{rule.Trigger}' → {rule.Action}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] Revoke failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Approved rules only — what the intent matcher sees.</summary>
        public async Task<IReadOnlyList<TeachingRuleView>> ActiveRulesAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                var rows = await db.TeachingRules
                    .Where(r => r.IsApproved)
                    .OrderBy(r => r.Id)
                    .ToListAsync();

                return rows.Select(Map).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] ActiveRules failed: {ex.Message}");
                return Array.Empty<TeachingRuleView>();
            }
        }

        /// <summary>Every rule for the Settings list (approved or not).</summary>
        public async Task<IReadOnlyList<TeachingRuleView>> AllRulesAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                var rows = await db.TeachingRules
                    .OrderByDescending(r => r.Id)
                    .ToListAsync();

                return rows.Select(Map).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] AllRules failed: {ex.Message}");
                return Array.Empty<TeachingRuleView>();
            }
        }

        /// <summary>Removes every taught rule. Audited.</summary>
        public async Task ClearTeachingAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                int count = await db.TeachingRules.CountAsync();

                db.TeachingRules.RemoveRange(db.TeachingRules);
                await db.SaveChangesAsync();

                _audit?.Teaching("", "Teaching cleared", $"{count} rule(s) removed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentTeaching] Clear failed: {ex.Message}");
            }
        }

        private static TeachingRuleView Map(TeachingRule r) => new()
        {
            Id = r.Id,
            Trigger = r.Trigger,
            Action = r.Action,
            IsApproved = r.IsApproved,
            ApprovedBy = r.ApprovedBy
        };

        private static string Cap(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
