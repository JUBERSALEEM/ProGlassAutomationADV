// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Memory/AgentMemoryService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// TWO TIERS, deliberately separate:
//
//   Conversation  — in-memory, capped at 50 turns, SESSION ONLY. Nothing
//                   the user says in passing is ever written to disk.
//   Approved      — persisted to agent.db, and ONLY through
//                   AddApprovedAsync, i.e. only when the user explicitly
//                   asked the agent to remember something.
//
// The two are cleared independently so "forget this conversation" and
// "forget everything you know about me" are different, honest actions.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Memory
{
    /// <summary>Session conversation + explicitly approved long-term facts.</summary>
    public sealed class AgentMemoryService : IAgentMemory
    {
        /// <summary>Hard cap on session turns held in RAM.</summary>
        public const int MaxConversationTurns = 50;

        private readonly List<string> _conversation = new();
        private readonly object _lock = new();
        private readonly IAgentAuditService? _audit;

        public AgentMemoryService(IAgentAuditService? audit = null)
        {
            _audit = audit;
            AgentDbContext.EnsureCreated();
        }

        /// <summary>Adds a turn. Oldest turns fall off past the cap.</summary>
        public void AddTurn(string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            lock (_lock)
            {
                _conversation.Add($"{role}: {text.Trim()}");

                while (_conversation.Count > MaxConversationTurns)
                    _conversation.RemoveAt(0);
            }
        }

        /// <summary>
        /// Persists a fact the user approved. This is the ONLY write path
        /// to disk in the memory subsystem.
        /// </summary>
        public async Task AddApprovedAsync(string kind, string content)
        {
            content = (content ?? "").Trim();
            if (content.Length == 0) return;

            try
            {
                using var db = new AgentDbContext();

                db.MemoryItems.Add(new MemoryItem
                {
                    Kind = Cap(string.IsNullOrWhiteSpace(kind) ? "note" : kind, 60),
                    Content = Cap(content, 2000),
                    CreatedBy = Services.Audit.AuditService.CurrentUsername,
                    CreatedAtUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync();

                _audit?.Teaching(
                    Services.Audit.AuditService.CurrentUsername,
                    "Memory saved",
                    Cap(content, 120));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentMemory] AddApproved failed: {ex.Message}");
            }
        }

        /// <summary>Session turns plus persisted facts, newest facts first.</summary>
        public async Task<IReadOnlyList<string>> RecallAsync(int max = 20)
        {
            var merged = new List<string>();

            try
            {
                using var db = new AgentDbContext();
                var facts = await db.MemoryItems
                    .OrderByDescending(m => m.Id)
                    .Take(max)
                    .ToListAsync();

                merged.AddRange(facts.Select(f => $"[{f.Kind}] {f.Content}"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentMemory] Recall (persisted) failed: {ex.Message}");
            }

            lock (_lock)
            {
                merged.AddRange(_conversation.TakeLast(max));
            }

            return merged;
        }

        /// <summary>Drops the session conversation. Disk is untouched.</summary>
        public void ClearConversation()
        {
            lock (_lock) _conversation.Clear();

            // The screen trail is session state too, so "clear the
            // conversation" must drop it as well. Leaving it behind would
            // mean the agent still knew where the user had been after being
            // told to forget - which is exactly the behaviour a privacy
            // control is supposed to prevent.
            ProGlassAutomation.Agent.Context.AgentScreenContext.Clear();
            ProGlassAutomation.Agent.Nlu.ConversationSlots.Clear();

            _audit?.Teaching("", "Conversation cleared",
                "session memory and screen trail only");
        }

        /// <summary>Drops persisted facts. The session is untouched.</summary>
        public async Task ClearPersistedAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                int count = await db.MemoryItems.CountAsync();

                db.MemoryItems.RemoveRange(db.MemoryItems);
                await db.SaveChangesAsync();

                _audit?.Teaching("", "Memory cleared", $"{count} saved item(s) removed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentMemory] ClearPersisted failed: {ex.Message}");
            }
        }

        private static string Cap(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
