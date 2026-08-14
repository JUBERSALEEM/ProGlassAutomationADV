// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIMemory/AiMemoryService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI — learning memory
//
// The AI "learns" the user's daily work by remembering exchanges:
// questions asked, answers given, entities discussed. Recent memories
// are appended to the AI context so the assistant builds continuity
// across sessions instead of starting blank every time.
//
// Privacy: memory is local-only (aimemory.db), can be cleared from
// Settings, and answers are truncated — no secrets are ever stored.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIMemory
{
    /// <summary>AI long-term memory facade. Never throws.</summary>
    public static class AiMemoryService
    {
        private const int MaxStored = 400;
        private const int MaxQuestionChars = 400;
        private const int MaxAnswerChars = 600;

        private static bool _initialized;

        /// <summary>Ensures aimemory.db exists. Call once at startup.</summary>
        public static async Task InitializeAsync()
        {
            if (_initialized) return;
            try
            {
                using var db = new AiMemoryDbContext();
                await db.Database.EnsureCreatedAsync();
                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AiMemory] Init failed: {ex.Message}");
            }
        }

        /// <summary>Stores one exchange (fire-and-forget safe).</summary>
        public static async Task RememberAsync(
            string question, string answer, string relatedEntityId = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(question)) return;

                using var db = new AiMemoryDbContext();

                db.Memories.Add(new AiMemoryEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Username = AuditService.CurrentUsername,
                    Question = Truncate(question, MaxQuestionChars),
                    AnswerSummary = Truncate(answer, MaxAnswerChars),
                    RelatedEntityId = relatedEntityId ?? ""
                });

                await db.SaveChangesAsync();
                await TrimAsync(db);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AiMemory] Remember failed: {ex.Message}");
            }
        }

        /// <summary>Newest-first memories for context building.</summary>
        public static async Task<List<AiMemoryEntry>> GetRecentAsync(int count = 12)
        {
            try
            {
                using var db = new AiMemoryDbContext();
                return await db.Memories
                    .OrderByDescending(m => m.TimestampUtc)
                    .Take(count)
                    .ToListAsync();
            }
            catch
            {
                return new List<AiMemoryEntry>();
            }
        }

        /// <summary>
        /// Formats recent memories as a prompt section. Empty string
        /// when nothing has been learned yet.
        /// </summary>
        public static async Task<string> BuildMemoryContextAsync()
        {
            var memories = await GetRecentAsync();
            if (memories.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("─── ASSISTANT MEMORY (previous interactions) ─────────");
            sb.AppendLine(
                "Use these to keep continuity with the user's daily work. " +
                "Never quote them back verbatim unless asked.");

            foreach (var m in memories)
            {
                sb.Append("• [")
                  .Append(m.TimestampUtc.ToLocalTime().ToString("dd MMM HH:mm"))
                  .Append("] Q: ").AppendLine(m.Question);

                if (!string.IsNullOrWhiteSpace(m.AnswerSummary))
                    sb.Append("  A: ").AppendLine(m.AnswerSummary);
            }

            return sb.ToString();
        }

        /// <summary>Clears all memory (Settings → privacy).</summary>
        public static async Task ClearAsync()
        {
            try
            {
                using var db = new AiMemoryDbContext();
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Memories");

                AuditService.Log(
                    AuditSections.Ai, "AI memory cleared",
                    "All assistant memories deleted");
            }
            catch { }
        }

        private static async Task TrimAsync(AiMemoryDbContext db)
        {
            int count = await db.Memories.CountAsync();
            if (count <= MaxStored) return;

            var oldest = await db.Memories
                .OrderBy(m => m.TimestampUtc)
                .Take(count - MaxStored)
                .ToListAsync();

            db.Memories.RemoveRange(oldest);
            await db.SaveChangesAsync();
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Trim();
            return value.Length <= max ? value : value[..max] + "…";
        }
    }
}
