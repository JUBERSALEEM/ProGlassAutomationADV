using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace ProGlassAutomation.Services.AI.AIMemory
{
    /// <summary>
    /// One remembered AI interaction. The assistant builds long-term
    /// understanding of the user's daily work from these rows — recent
    /// memories are injected into the AI context on every question.
    /// </summary>
    public class AiMemoryEntry
    {
        public int Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Username { get; set; } = "";

        /// <summary>What the user asked.</summary>
        public string Question { get; set; } = "";

        /// <summary>Shortened form of what the assistant answered.</summary>
        public string AnswerSummary { get; set; } = "";

        /// <summary>Entity referenced in the exchange, when detected (PI/JO/DN/TI id).</summary>
        public string RelatedEntityId { get; set; } = "";
    }

    /// <summary>Dedicated SQLite DB for AI memory: aimemory.db.</summary>
    public class AiMemoryDbContext : DbContext
    {
        public DbSet<AiMemoryEntry> Memories { get; set; }

        public static string DatabaseFilePath => Path.Combine(
            ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
            "aimemory.db");

        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            // Shared-aware folder ensure (2026-09-24) — see AppDataRoot.
            ProGlassAutomation.Services.Database.AppDataRoot.EnsureCurrentFolder();

            // Senior audit (Aug 2026): route through SqliteResilience for WAL,
            // busy_timeout, foreign_keys=ON. See SqliteResilience.cs.
            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(optionsBuilder, DatabaseFilePath);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AiMemoryEntry>(e =>
            {
                e.HasIndex(m => m.TimestampUtc);
                e.HasIndex(m => m.Username);
            });
        }
    }
}
